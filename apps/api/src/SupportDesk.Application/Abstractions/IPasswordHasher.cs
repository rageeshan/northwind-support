namespace SupportDesk.Application.Abstractions;

/// <summary>
/// One-way password hashing. Implementations must use a salted, vetted algorithm
/// (Identity <c>PasswordHasher</c>, BCrypt or PBKDF2) — never a home-made hash.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Verifies <paramref name="password"/> against <paramref name="passwordHash"/>.
    /// A null or empty hash still runs a dummy verify so missing accounts take a similar
    /// amount of time as real ones.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}
