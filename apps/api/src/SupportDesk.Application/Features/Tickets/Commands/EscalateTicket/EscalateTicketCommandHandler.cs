using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket one priority level (BR-7):
/// raises priority, recomputes the SLA window from now, re-evaluates agent assignment,
/// and appends an immutable escalation history row.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentTriageQueries agentTriageQueries,
    IUnitOfWork unitOfWork,
    IClock clock,
    TriageService triageService,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">Ticket does not exist.</exception>
    /// <exception cref="Domain.Exceptions.BusinessRuleViolationException">
    ///   Ticket is Critical, Resolved, or Closed (→ 409).
    /// </exception>
    public async Task<TicketDetailDto> HandleAsync(
        int ticketId,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        // Re-triage at the new (escalated) priority for the SLA window and agent selection.
        // TriageService.Decide will force Critical when the category mandates it, but since
        // we are escalating we pass the already-escalated priority directly.
        var candidates = await agentTriageQueries.GetCandidatesAsync(ct);
        var newPriority = ticket.Priority + 1; // validated by Ticket.Escalate below
        var decision = triageService.Decide(newPriority, category, customer, candidates);

        var newDueAtUtc = now + decision.SlaWindow;

        // Domain guard: throws BusinessRuleViolationException when Critical / Resolved / Closed.
        var snapshot = ticket.Escalate(decision.AssignedAgentId, newDueAtUtc, now);

        var escalation = new TicketEscalation(
            ticketId: ticket.Id,
            fromPriority: snapshot.FromPriority,
            toPriority: snapshot.ToPriority,
            fromAgentId: snapshot.FromAgentId,
            toAgentId: snapshot.ToAgentId,
            fromDueAtUtc: snapshot.FromDueAtUtc,
            toDueAtUtc: snapshot.ToDueAtUtc,
            reason: request.Reason,
            escalatedBy: request.EscalatedBy,
            escalatedAtUtc: now);

        await escalations.AddAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {From} to {To} by {Who}.",
            ticket.Reference, snapshot.FromPriority, snapshot.ToPriority, request.EscalatedBy);

        return await getTicket.HandleAsync(ticket.Id, ct);
    }
}
