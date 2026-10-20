using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>
/// Returns the signed-in agent. Inactive or missing agents are treated as unauthenticated
/// so a deactivated account cannot keep using an old token against /me.
/// </summary>
public sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser, IAgentRepository agents)
{
    public async Task<AuthUserDto> HandleAsync(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException("Not authenticated.");
        }

        var agent = await agents.GetByIdAsync(currentUser.AgentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("Not authenticated.");
        }

        return new AuthUserDto(agent.Id, agent.FullName, agent.Email);
    }
}
