using SupportDesk.Domain.Aggregates.Tickets;
using Xunit;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class SlaEvaluatorAtRiskTests
{
    private static readonly DateTime Start = new(2024, 6, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Due   = Start.AddHours(8);  // 8-hour window

    // 25% threshold: AtRisk when ≤ 2 hours remain (25% of 8h = 2h)
    [Fact]
    public void Evaluate_WhenTimeRemainingBelowThreshold_ReturnsAtRisk()
    {
        var now = Due.AddMinutes(-90); // 1.5h remaining → 18.75% of 8h → below 25%

        var status = SlaEvaluator.Evaluate(Due, null, now, slaPeriodStartUtc: Start, atRiskThresholdPercent: 25);

        Assert.Equal(SlaStatus.AtRisk, status);
    }

    [Fact]
    public void Evaluate_WhenTimeRemainingAboveThreshold_ReturnsWithinSla()
    {
        var now = Due.AddMinutes(-300); // 5h remaining → 62.5% of 8h → above 25%

        var status = SlaEvaluator.Evaluate(Due, null, now, slaPeriodStartUtc: Start, atRiskThresholdPercent: 25);

        Assert.Equal(SlaStatus.WithinSla, status);
    }

    [Fact]
    public void Evaluate_WhenExactlyAtThresholdBoundary_ReturnsAtRisk()
    {
        var now = Due.AddMinutes(-120); // 2h remaining = exactly 25% of 8h

        var status = SlaEvaluator.Evaluate(Due, null, now, slaPeriodStartUtc: Start, atRiskThresholdPercent: 25);

        Assert.Equal(SlaStatus.AtRisk, status);
    }

    [Fact]
    public void Evaluate_WhenNoDueDate_ReturnsNotApplicable()
    {
        var status = SlaEvaluator.Evaluate(null, null, Start, slaPeriodStartUtc: Start, atRiskThresholdPercent: 25);

        Assert.Equal(SlaStatus.NotApplicable, status);
    }

    [Fact]
    public void Evaluate_WhenResolvedBeforeDue_ReturnsMet()
    {
        var resolved = Due.AddHours(-1);

        var status = SlaEvaluator.Evaluate(Due, resolved, Due, slaPeriodStartUtc: Start, atRiskThresholdPercent: 25);

        Assert.Equal(SlaStatus.Met, status);
    }

    [Fact]
    public void Evaluate_WhenNowPastDueAndOpen_ReturnsBreached()
    {
        var now = Due.AddMinutes(1);

        var status = SlaEvaluator.Evaluate(Due, null, now, slaPeriodStartUtc: Start, atRiskThresholdPercent: 25);

        Assert.Equal(SlaStatus.Breached, status);
    }

    [Fact]
    public void Evaluate_WithZeroThreshold_NeverReturnsAtRisk()
    {
        var now = Due.AddMinutes(-1); // 1 minute left — would normally be AtRisk

        var status = SlaEvaluator.Evaluate(Due, null, now, slaPeriodStartUtc: Start, atRiskThresholdPercent: 0);

        Assert.Equal(SlaStatus.WithinSla, status);
    }

    [Fact]
    public void Evaluate_WithNullSlaPeriodStart_NeverReturnsAtRisk()
    {
        var now = Due.AddMinutes(-1);

        var status = SlaEvaluator.Evaluate(Due, null, now, slaPeriodStartUtc: null, atRiskThresholdPercent: 25);

        Assert.Equal(SlaStatus.WithinSla, status);
    }
}
