namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when authentication fails or the caller is not signed in. Surfaces as HTTP 401.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    public UnauthorizedException(string message) : base(message)
    {
    }

    public static UnauthorizedException InvalidCredentials() =>
        new(InvalidCredentialsMessage);
}
