using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>Issues a signed JWT for a successfully authenticated agent.</summary>
public interface ITokenService
{
    AuthToken Issue(Agent agent);
}
