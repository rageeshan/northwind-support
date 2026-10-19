using SupportDesk.Application.Services;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Read-only triage query: returns all active agents with their current open-ticket count and
/// specializations, so the <see cref="TriageService"/> can select the best owner without going
/// through the write-side repositories.
/// </summary>
public interface IAgentTriageQueries
{
    /// <summary>
    /// Returns every agent record that might be eligible for assignment, together with the
    /// ids of the categories they are specialised in and their current open-ticket count.
    /// Inactive agents are excluded at the database level.
    /// </summary>
    Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(CancellationToken ct);
}
