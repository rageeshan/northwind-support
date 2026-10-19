using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using Xunit;

namespace SupportDesk.UnitTests.Application.Services;

public sealed class TriageServiceTests
{
    private static SlaOptions DefaultOptions() => new()
    {
        BaseWindowHours = new Dictionary<string, double>
        {
            ["Critical"] = 4,
            ["High"]     = 8,
            ["Medium"]   = 24,
            ["Low"]      = 72,
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };

    private static Category MakeCategory(bool forcesCritical = false, bool requiresSpecialist = false)
    {
        // Use reflection to bypass EF private setters for test purposes.
        var c = (Category)Activator.CreateInstance(typeof(Category), nonPublic: true)!;
        SetProp(c, nameof(Category.Id), 1);
        SetProp(c, nameof(Category.Name), "Billing");
        SetProp(c, nameof(Category.ForcesCriticalPriority), forcesCritical);
        SetProp(c, nameof(Category.RequiresSpecialist), requiresSpecialist);
        SetProp(c, nameof(Category.IsActive), true);
        return c;
    }

    private static Customer MakeCustomer(CustomerTier tier)
    {
        var c = (Customer)Activator.CreateInstance(typeof(Customer), nonPublic: true)!;
        SetProp(c, nameof(Customer.Id), 99);
        SetProp(c, nameof(Customer.Name), "Acme");
        SetProp(c, nameof(Customer.Tier), tier);
        return c;
    }

    private static AgentCandidate MakeAgent(int id, int openCount = 0, int[]? categoryIds = null, bool isActive = true) =>
        new AgentCandidate(id, $"Agent {id}", isActive, MaxOpenTickets: 10, OpenTicketCount: openCount,
            SpecializationCategoryIds: categoryIds ?? []);

    private static void SetProp(object obj, string name, object value)
    {
        var prop = obj.GetType().GetProperty(name)
            ?? throw new InvalidOperationException($"{name} not found on {obj.GetType().Name}");
        prop.SetValue(obj, value);
    }

    // ─── BR-1: Category forces Critical priority ─────────────────────────────

    [Fact]
    public void Decide_WhenCategoryForcesCritical_OverridesRequestedPriority()
    {
        var service = new TriageService(DefaultOptions());
        var category = MakeCategory(forcesCritical: true);
        var customer = MakeCustomer(CustomerTier.Standard);

        var result = service.Decide(TicketPriority.Low, category, customer, []);

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Contains("forces Critical", result.PriorityReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decide_WhenCategoryDoesNotForceCritical_HonorsRequestedPriority()
    {
        var service = new TriageService(DefaultOptions());
        var category = MakeCategory(forcesCritical: false);
        var customer = MakeCustomer(CustomerTier.Standard);

        var result = service.Decide(TicketPriority.High, category, customer, []);

        Assert.Equal(TicketPriority.High, result.Priority);
    }

    // ─── BR-2: SLA window from options ────────────────────────────────────────

    [Theory]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.High,     CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Medium,   CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.Low,      CustomerTier.Standard, 72)]
    public void Decide_StandardCustomer_UsesBaseWindow(TicketPriority priority, CustomerTier tier, double expectedHours)
    {
        var service = new TriageService(DefaultOptions());
        var category = MakeCategory();
        var customer = MakeCustomer(tier);

        var result = service.Decide(priority, category, customer, []);

        Assert.Equal(TimeSpan.FromHours(expectedHours), result.SlaWindow);
    }

    // ─── BR-3: Premium customer halved window ─────────────────────────────────

    [Theory]
    [InlineData(TicketPriority.High, 4)]   // 8 * 0.5
    [InlineData(TicketPriority.Medium, 12)] // 24 * 0.5
    [InlineData(TicketPriority.Low, 36)]   // 72 * 0.5
    public void Decide_PremiumCustomer_HalvesWindow(TicketPriority priority, double expectedHours)
    {
        var service = new TriageService(DefaultOptions());
        var category = MakeCategory();
        var customer = MakeCustomer(CustomerTier.Premium);

        var result = service.Decide(priority, category, customer, []);

        Assert.Equal(TimeSpan.FromHours(expectedHours), result.SlaWindow);
    }

    [Fact]
    public void Decide_PremiumCritical_IsConstrainedByMinimumFloor()
    {
        var opts = new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, double>
            {
                ["Critical"] = 4, ["High"] = 8, ["Medium"] = 24, ["Low"] = 72
            },
            PremiumCustomerMultiplier = 0.1,
            MinimumWindowHours = 2,
            AtRiskThresholdPercent = 25
        };
        var service = new TriageService(opts);
        // 4 * 0.1 = 0.4 hours → floored to 2 hours
        var result = service.Decide(TicketPriority.Critical, MakeCategory(), MakeCustomer(CustomerTier.Premium), []);

        Assert.Equal(TimeSpan.FromHours(2), result.SlaWindow);
    }


    // ─── BR-4 / BR-5: Agent selection ─────────────────────────────────────────

    [Fact]
    public void Decide_SelectsAgentWithFewestOpenTickets()
    {
        var service = new TriageService(DefaultOptions());
        var agents = new[]
        {
            MakeAgent(1, openCount: 5),
            MakeAgent(2, openCount: 2),
            MakeAgent(3, openCount: 8),
        };

        var result = service.Decide(TicketPriority.Medium, MakeCategory(), MakeCustomer(CustomerTier.Standard), agents);

        Assert.Equal(2, result.AssignedAgentId);
    }

    [Fact]
    public void Decide_SkipsAgentsAtCapacity()
    {
        var service = new TriageService(DefaultOptions());
        // Agent 1 is at max (10 open / max 10); agent 2 has capacity.
        var agents = new[]
        {
            new AgentCandidate(1, "Full Agent", true, 10, 10, []),
            MakeAgent(2, openCount: 3),
        };

        var result = service.Decide(TicketPriority.Medium, MakeCategory(), MakeCustomer(CustomerTier.Standard), agents);

        Assert.Equal(2, result.AssignedAgentId);
    }

    [Fact]
    public void Decide_SkipsInactiveAgents()
    {
        var service = new TriageService(DefaultOptions());
        var agents = new[]
        {
            MakeAgent(1, isActive: false),
            MakeAgent(2, openCount: 3),
        };

        var result = service.Decide(TicketPriority.Medium, MakeCategory(), MakeCustomer(CustomerTier.Standard), agents);

        Assert.Equal(2, result.AssignedAgentId);
    }

    [Fact]
    public void Decide_WhenRequiresSpecialist_OnlyPicksSpecialisedAgent()
    {
        var service = new TriageService(DefaultOptions());
        var category = MakeCategory(requiresSpecialist: true); // id=1
        var agents = new[]
        {
            MakeAgent(1, categoryIds: [42]),    // wrong category
            MakeAgent(2, categoryIds: [1, 99]), // matches
        };

        var result = service.Decide(TicketPriority.High, category, MakeCustomer(CustomerTier.Standard), agents);

        Assert.Equal(2, result.AssignedAgentId);
    }

    [Fact]
    public void Decide_WhenNoEligibleAgent_ReturnsNullAssignment()
    {
        var service = new TriageService(DefaultOptions());
        var result = service.Decide(TicketPriority.Medium, MakeCategory(), MakeCustomer(CustomerTier.Standard), []);

        Assert.Null(result.AssignedAgentId);
    }
}
