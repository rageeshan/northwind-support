using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer, running the full triage (BR-1 through BR-5) immediately
/// so the ticket starts with the correct priority, due date, and owner.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentTriageQueries agentTriageQueries,
    IUnitOfWork unitOfWork,
    IClock clock,
    TriageService triageService,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            request.RequestedPriority ?? TicketPriority.Medium,
            now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct); // needed so ticket.Id is set before triage

        // Run triage now that the ticket has a database id.
        var candidates = await agentTriageQueries.GetCandidatesAsync(ct);
        var decision = triageService.Decide(
            request.RequestedPriority ?? TicketPriority.Medium,
            category,
            customer,
            candidates);

        var dueAtUtc = now + decision.SlaWindow;
        ticket.Triage(decision.Priority, decision.AssignedAgentId, dueAtUtc, now);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId}: {Priority}, due {DueAtUtc}, agent {AgentId}.",
            ticket.Reference, ticket.CustomerId, decision.Priority, dueAtUtc, decision.AssignedAgentId);

        var detail = await getTicket.HandleAsync(ticket.Id, ct);

        // Attach the triage narrative so the UI can show what the system decided.
        detail.TriageResult = new TriageResultDto
        {
            Priority = decision.Priority,
            PriorityReason = decision.PriorityReason,
            SlaWindowHours = decision.SlaWindow.TotalHours,
            SlaWindowReason = decision.SlaWindowReason,
            AssignmentReason = decision.AssignmentReason
        };

        return detail;
    }
}
