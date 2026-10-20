using System.Text;

namespace SupportDesk.Presentation.Authentication;

/// <summary>JWT settings. Bound from the Jwt configuration section / Jwt__* environment variables.</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>Eight hours: a typical support-desk shift. Configurable without a code change.</summary>
    public const int DefaultLifetimeMinutes = 480;

    public string Issuer { get; set; } = "northwind-support";

    public string Audience { get; set; } = "northwind-support-web";

    public string SigningKey { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = DefaultLifetimeMinutes;

    public void EnsureValid()
    {
        if (Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256. " +
                "Set Jwt__SigningKey (see .env.example). Do not commit a real secret.");
        }

        if (LifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("Jwt:LifetimeMinutes must be a positive number of minutes.");
        }
    }
}
