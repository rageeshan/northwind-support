using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using Xunit;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class TicketEscalationTests
{
    private static DateTime Now => new(2024, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Ticket RaiseTicket(TicketPriority priority = TicketPriority.Medium)
    {
        var ticket = Ticket.Raise("SD-001", "Test title", "Test description that is long enough", 1, 1, priority, Now);
        // Give it a db id via reflection (the handler would normally do a SaveChanges first).
        typeof(Ticket).BaseType!.GetProperty("Id")!.SetValue(ticket, 1);
        return ticket;
    }

    [Fact]
    public void Triage_SetsAllFields()
    {
        var ticket = RaiseTicket();
        var due = Now.AddHours(8);

        ticket.Triage(TicketPriority.High, agentId: 42, dueAtUtc: due, nowUtc: Now);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(42, ticket.AssignedAgentId);
        Assert.Equal(due, ticket.DueAtUtc);
        Assert.Equal(Now, ticket.SlaPeriodStartUtc);
    }

    [Fact]
    public void Triage_WithNoDueDate_LeavesSlaPeriodStartNull()
    {
        var ticket = RaiseTicket();

        ticket.Triage(TicketPriority.Medium, agentId: null, dueAtUtc: null, nowUtc: Now);

        Assert.Null(ticket.SlaPeriodStartUtc);
    }

    [Fact]
    public void Escalate_RaisesPriorityByOne()
    {
        var ticket = RaiseTicket(TicketPriority.Medium);
        var due = Now.AddHours(4);

        ticket.Escalate(newAgentId: null, newDueAtUtc: due, nowUtc: Now);

        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    [Fact]
    public void Escalate_ReturnsCorrectSnapshot()
    {
        var ticket = RaiseTicket(TicketPriority.Low);
        ticket.Triage(TicketPriority.Low, agentId: 1, dueAtUtc: Now.AddHours(72), nowUtc: Now);

        var snapshot = ticket.Escalate(newAgentId: 2, newDueAtUtc: Now.AddHours(8), nowUtc: Now);

        Assert.Equal(TicketPriority.Low, snapshot.FromPriority);
        Assert.Equal(TicketPriority.Medium, snapshot.ToPriority);
        Assert.Equal(1, snapshot.FromAgentId);
        Assert.Equal(2, snapshot.ToAgentId);
    }

    [Fact]
    public void Escalate_ResetsSlaPeriodStart()
    {
        var ticket = RaiseTicket(TicketPriority.Medium);
        ticket.Triage(TicketPriority.Medium, agentId: null, dueAtUtc: Now.AddHours(24), nowUtc: Now);

        var laterNow = Now.AddHours(6);
        var newDue = laterNow.AddHours(8);
        ticket.Escalate(newAgentId: null, newDueAtUtc: newDue, nowUtc: laterNow);

        // SlaPeriodStartUtc resets to the escalation timestamp, not the original creation time.
        Assert.Equal(laterNow, ticket.SlaPeriodStartUtc);
    }

    [Fact]
    public void Escalate_WhenAlreadyCritical_ThrowsBusinessRuleViolation()
    {
        var ticket = RaiseTicket(TicketPriority.Critical);

        Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(newAgentId: null, newDueAtUtc: Now.AddHours(2), nowUtc: Now));
    }

    [Fact]
    public void Escalate_WhenResolved_ThrowsBusinessRuleViolation()
    {
        var ticket = RaiseTicket(TicketPriority.Medium);
        ticket.ChangeStatus(TicketStatus.Resolved, Now);

        Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(newAgentId: null, newDueAtUtc: Now.AddHours(8), nowUtc: Now));
    }

    [Fact]
    public void Escalate_WhenClosed_ThrowsBusinessRuleViolation()
    {
        var ticket = RaiseTicket(TicketPriority.Medium);
        ticket.ChangeStatus(TicketStatus.Resolved, Now);
        ticket.ChangeStatus(TicketStatus.Closed, Now);

        Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(newAgentId: null, newDueAtUtc: Now.AddHours(8), nowUtc: Now));
    }
}
