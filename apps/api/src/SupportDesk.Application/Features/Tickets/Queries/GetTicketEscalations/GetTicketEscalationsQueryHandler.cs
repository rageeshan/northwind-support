using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>
/// Returns the escalation history for a ticket, newest first.
/// </summary>
public sealed class GetTicketEscalationsQueryHandler(
    ITicketEscalationRepository escalations,
    ITicketRepository tickets)
{
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    public async Task<IReadOnlyList<EscalationDto>> HandleAsync(int ticketId, CancellationToken ct)
    {
        // Verify the ticket exists (returns 404 rather than an empty list when the id is wrong).
        var exists = await tickets.GetByIdAsync(ticketId, ct);
        if (exists is null)
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        var rows = await escalations.GetForTicketAsync(ticketId, ct);

        return rows.Select(e => new EscalationDto
        {
            Id = e.Id,
            FromPriority = e.FromPriority,
            ToPriority = e.ToPriority,
            FromAgent = e.FromAgentId is not null ? new AgentSummaryDto(e.FromAgentId.Value, string.Empty) : null,
            ToAgent = e.ToAgentId is not null ? new AgentSummaryDto(e.ToAgentId.Value, string.Empty) : null,
            FromDueAtUtc = e.FromDueAtUtc,
            ToDueAtUtc = e.ToDueAtUtc,
            Reason = e.Reason,
            EscalatedBy = e.EscalatedBy,
            EscalatedAtUtc = e.EscalatedAtUtc
        }).ToList();
    }
}
