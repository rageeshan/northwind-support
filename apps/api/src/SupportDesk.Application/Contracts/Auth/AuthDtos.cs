namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in agent, enough for the UI to greet them.</summary>
public sealed record AuthUserDto(int Id, string FullName, string Email);

/// <summary>A JWT plus when it expires.</summary>
public sealed record AuthToken(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>Body of a successful login.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, AuthUserDto User);
