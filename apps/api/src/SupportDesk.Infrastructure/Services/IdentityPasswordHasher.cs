using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// ASP.NET Identity's PBKDF2 password hasher (v3). Salted, iterated, not reversible.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _user = new();
    private readonly string _dummyHash;

    public IdentityPasswordHasher()
    {
        _dummyHash = _hasher.HashPassword(_user, "timing-equalisation-dummy");
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(_user, password);
    }

    public bool Verify(string password, string? passwordHash)
    {
        var hash = string.IsNullOrEmpty(passwordHash) ? _dummyHash : passwordHash;

        try
        {
            var result = _hasher.VerifyHashedPassword(_user, hash, password);
            return result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
