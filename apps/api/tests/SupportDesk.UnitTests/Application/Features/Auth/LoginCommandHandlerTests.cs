using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly LoginCommandHandler _sut;
    private readonly Agent _alex;

    public LoginCommandHandlerTests()
    {
        _alex = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, FixedClock.DefaultNow);
        _alex.SetPasswordHash("hashed-password");

        _sut = new LoginCommandHandler(_agents.Object, _hasher.Object, _tokens.Object);
    }

    [Fact]
    public async Task Wrong_password_throws_the_same_401_as_an_unknown_email_and_issues_no_token()
    {
        _agents.Setup(a => a.GetByEmailAsync(_alex.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_alex);
        _hasher.Setup(h => h.Verify("wrong-password", _alex.PasswordHash)).Returns(false);

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.HandleAsync(new LoginRequest(_alex.Email, "wrong-password"), CancellationToken.None));

        _agents.Setup(a => a.GetByEmailAsync("nobody@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);
        _hasher.Setup(h => h.Verify("any-password", null)).Returns(false);

        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.HandleAsync(new LoginRequest("nobody@example.com", "any-password"), CancellationToken.None));

        Assert.Equal(UnauthorizedException.InvalidCredentialsMessage, wrongPassword.Message);
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task Inactive_agent_cannot_log_in()
    {
        _alex.Deactivate();
        _agents.Setup(a => a.GetByEmailAsync(_alex.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_alex);
        _hasher.Setup(h => h.Verify("Local_Dev_Password1!", _alex.PasswordHash)).Returns(true);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.HandleAsync(new LoginRequest(_alex.Email, "Local_Dev_Password1!"), CancellationToken.None));

        Assert.Equal(UnauthorizedException.InvalidCredentialsMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task Successful_login_returns_a_token_and_the_agent_to_greet()
    {
        _agents.Setup(a => a.GetByEmailAsync(_alex.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_alex);
        _hasher.Setup(h => h.Verify("Local_Dev_Password1!", _alex.PasswordHash)).Returns(true);
        _tokens.Setup(t => t.Issue(_alex))
            .Returns(new AuthToken("jwt-token", FixedClock.DefaultNow.AddHours(8)));

        var result = await _sut.HandleAsync(
            new LoginRequest(_alex.Email, "Local_Dev_Password1!"),
            CancellationToken.None);

        Assert.Equal("jwt-token", result.AccessToken);
        Assert.Equal(_alex.FullName, result.User.FullName);
        Assert.Equal(_alex.Email, result.User.Email);
        Assert.NotEqual(default, result.ExpiresAtUtc);
    }
}
