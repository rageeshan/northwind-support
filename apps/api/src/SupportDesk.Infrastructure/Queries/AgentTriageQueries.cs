using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Fetches all active agents with their open-ticket counts and specializations for use by
/// <see cref="Application.Services.TriageService"/>. Read-only: nothing is tracked.
/// </summary>
public sealed class AgentTriageQueries(SupportDbContext db) : IAgentTriageQueries
{
    public async Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(CancellationToken ct)
    {
        var openStatuses = new[] { TicketStatus.Resolved, TicketStatus.Closed };

        var rows = await db.Set<Agent>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .Select(a => new
            {
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                OpenTicketCount = db.Set<Domain.Aggregates.Tickets.Ticket>()
                    .Count(t => t.AssignedAgentId == a.Id && !openStatuses.Contains(t.Status)),
                SpecializationCategoryIds = a.Specializations.Select(s => s.CategoryId).ToList()
            })
            .ToListAsync(ct);

        return rows.Select(r => new AgentCandidate(
            r.Id,
            r.FullName,
            r.IsActive,
            r.MaxOpenTickets,
            r.OpenTicketCount,
            r.SpecializationCategoryIds)).ToList();
    }
}
