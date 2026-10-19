namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    /// <summary>
    /// Evaluates the SLA status. Pass <paramref name="slaPeriodStartUtc"/> and
    /// <paramref name="atRiskThresholdPercent"/> so that the AtRisk band can be computed;
    /// omit them and every unresolved, in-window ticket is reported as <see cref="SlaStatus.WithinSla"/>.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime? slaPeriodStartUtc = null,
        double atRiskThresholdPercent = 0)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        // Ticket is open and inside the window. Check whether it is at risk.
        if (atRiskThresholdPercent > 0 && slaPeriodStartUtc is not null)
        {
            var totalWindow = (dueAtUtc.Value - slaPeriodStartUtc.Value).TotalMinutes;
            var remaining = (dueAtUtc.Value - nowUtc).TotalMinutes;

            if (totalWindow > 0 && remaining / totalWindow * 100 <= atRiskThresholdPercent)
            {
                return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }
}

