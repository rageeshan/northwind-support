using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in. Wrong email, wrong password and inactive accounts all fail
/// with the same 401 so the response does not leak which case it was.
/// </summary>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenService tokens)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email, ct);

        // Verify even when the agent is missing so the work is similar either way.
        var passwordOk = passwordHasher.Verify(request.Password, agent?.PasswordHash);

        if (agent is null || !passwordOk || !agent.IsActive)
        {
            throw UnauthorizedException.InvalidCredentials();
        }

        var token = tokens.Issue(agent);
        var user = new AuthUserDto(agent.Id, agent.FullName, agent.Email);

        return new LoginResponse(token.AccessToken, token.ExpiresAtUtc, user);
    }
}
