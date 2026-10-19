using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class TicketEscalationRepository(SupportDbContext db) : ITicketEscalationRepository
{
    public Task AddAsync(TicketEscalation escalation, CancellationToken ct)
    {
        db.Add(escalation);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketEscalation>> GetForTicketAsync(int ticketId, CancellationToken ct) =>
        await db.Set<TicketEscalation>()
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId)
            .OrderByDescending(e => e.EscalatedAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);
}
