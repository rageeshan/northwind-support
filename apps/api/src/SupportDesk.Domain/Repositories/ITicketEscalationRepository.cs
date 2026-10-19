using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Stores and retrieves <see cref="TicketEscalation"/> records.
/// </summary>
public interface ITicketEscalationRepository
{
    Task AddAsync(TicketEscalation escalation, CancellationToken ct);

    /// <summary>Returns the escalation history for a ticket, newest first.</summary>
    Task<IReadOnlyList<TicketEscalation>> GetForTicketAsync(int ticketId, CancellationToken ct);
}
