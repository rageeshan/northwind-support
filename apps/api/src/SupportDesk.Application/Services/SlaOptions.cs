using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

/// <summary>
/// Configurable SLA response windows. Bound from the "Sla" section of appsettings.
/// </summary>
public sealed class SlaOptions
{
    public const string Section = "Sla";

    /// <summary>Base hours per priority name, e.g. { "Critical": 4, "High": 8, ... }.</summary>
    public Dictionary<string, double> BaseWindowHours { get; init; } = new();

    /// <summary>
    /// The fraction of the base window given to Premium customers (default 0.5 = half).
    /// </summary>
    public double PremiumCustomerMultiplier { get; init; } = 0.5;

    /// <summary>The absolute floor, in hours, regardless of tier or multiplier.</summary>
    public double MinimumWindowHours { get; init; } = 1;

    /// <summary>
    /// Percentage of the window remaining at which a ticket is classified AtRisk (default 25).
    /// </summary>
    public double AtRiskThresholdPercent { get; init; } = 25;

    /// <summary>
    /// Returns the SLA response window for the given priority and customer tier,
    /// applying the Premium multiplier and the minimum floor.
    /// </summary>
    public TimeSpan GetWindow(TicketPriority priority, CustomerTier tier)
    {
        var key = priority.ToString();

        var baseHours = BaseWindowHours.TryGetValue(key, out var h) ? h : 24;

        var hours = tier == CustomerTier.Premium
            ? Math.Max(baseHours * PremiumCustomerMultiplier, MinimumWindowHours)
            : baseHours;

        return TimeSpan.FromHours(hours);
    }
}
