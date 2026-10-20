using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Looks up an agent by email, ignoring case and surrounding whitespace.</summary>
    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);
}
