using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Tests that <see cref="TicketQueries.GetPagedAsync"/> applies every filter in the database
/// before counting and paging. Uses a SQLite in-memory database so the query translation is
/// exercised; no SQL Server is required.
/// </summary>
public sealed class TicketQueriesFilteringTests : IAsyncLifetime
{
    private readonly SupportDbContext _db;
    private readonly TicketQueries _sut;

    // Shared reference data (ids will be assigned by EF after SaveChanges).
    private Customer _contoso = default!;
    private Customer _fabrikam = default!;
    private Category _billing = default!;
    private Category _general = default!;
    private Agent _agentAlex = default!;

    public TicketQueriesFilteringTests()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new SupportDbContext(options);
        _sut = new TicketQueries(_db, new FixedClock(), new SupportDesk.Application.Services.SlaOptions());

    }

    public async Task InitializeAsync()
    {
        // SQLite in-memory: open the connection before EnsureCreated so it stays alive.
        await _db.Database.OpenConnectionAsync();
        await _db.Database.EnsureCreatedAsync();

        _contoso = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, FixedClock.DefaultNow);
        _fabrikam = new Customer("Fabrikam Inc", "support@fabrikam.example", null, CustomerTier.Standard, FixedClock.DefaultNow);
        _billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _general = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _agentAlex = new Agent("Alex Turner", "alex@example.com", maxOpenTickets: 10, FixedClock.DefaultNow);

        _db.AddRange(_contoso, _fabrikam, _billing, _general, _agentAlex);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // FL-1: Exact-match filters
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Priority_filter_returns_only_tickets_with_that_priority()
    {
        await AddTicketAsync("High ticket", _contoso.Id, _general.Id, TicketPriority.High);
        await AddTicketAsync("Low ticket", _contoso.Id, _general.Id, TicketPriority.Low);
        await AddTicketAsync("Another High", _fabrikam.Id, _general.Id, TicketPriority.High);

        var query = new TicketQuery { Priority = TicketPriority.High, Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task Status_filter_returns_only_tickets_with_that_status()
    {
        var open = await AddTicketAsync("Open ticket", _contoso.Id, _general.Id, TicketPriority.Medium);
        var inProgress = await AddTicketAsync("InProgress ticket", _contoso.Id, _general.Id, TicketPriority.Medium);
        open.ChangeStatus(TicketStatus.Open, FixedClock.DefaultNow);
        inProgress.ChangeStatus(TicketStatus.InProgress, FixedClock.DefaultNow);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { Status = TicketStatus.InProgress, Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(TicketStatus.InProgress, result.Items[0].Status);
    }

    [Fact]
    public async Task CategoryId_filter_returns_only_tickets_in_that_category()
    {
        await AddTicketAsync("Billing ticket", _contoso.Id, _billing.Id, TicketPriority.Medium);
        await AddTicketAsync("General ticket", _contoso.Id, _general.Id, TicketPriority.Medium);

        var query = new TicketQuery { CategoryId = _billing.Id, Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_billing.Id, result.Items[0].Category.Id);
    }

    [Fact]
    public async Task CustomerId_filter_returns_only_tickets_for_that_customer()
    {
        await AddTicketAsync("Contoso ticket", _contoso.Id, _general.Id, TicketPriority.Medium);
        await AddTicketAsync("Fabrikam ticket", _fabrikam.Id, _general.Id, TicketPriority.Medium);

        var query = new TicketQuery { CustomerId = _fabrikam.Id, Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Fabrikam Inc", result.Items[0].Customer.Name);
    }

    [Fact]
    public async Task AssignedAgentId_filter_returns_only_tickets_assigned_to_that_agent()
    {
        var assigned = await AddTicketAsync("Assigned ticket", _contoso.Id, _general.Id, TicketPriority.Medium);
        assigned.AssignTo(_agentAlex.Id, FixedClock.DefaultNow);
        await AddTicketAsync("Unassigned ticket", _fabrikam.Id, _general.Id, TicketPriority.Medium);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { AssignedAgentId = _agentAlex.Id, Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_agentAlex.Id, result.Items[0].AssignedAgent!.Id);
    }

    // -------------------------------------------------------------------------
    // FL-2: unassignedOnly
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UnassignedOnly_returns_only_tickets_with_no_assigned_agent()
    {
        var assigned = await AddTicketAsync("Assigned", _contoso.Id, _general.Id, TicketPriority.Medium);
        assigned.AssignTo(_agentAlex.Id, FixedClock.DefaultNow);
        await AddTicketAsync("Unassigned 1", _fabrikam.Id, _general.Id, TicketPriority.Low);
        await AddTicketAsync("Unassigned 2", _contoso.Id, _billing.Id, TicketPriority.High);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { UnassignedOnly = true, Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Null(item.AssignedAgent));
    }

    // -------------------------------------------------------------------------
    // FL-3: Search
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Search_matches_customer_name()
    {
        await AddTicketAsync("Ticket A", _contoso.Id, _general.Id, TicketPriority.Medium);
        await AddTicketAsync("Ticket B", _fabrikam.Id, _general.Id, TicketPriority.Medium);

        var query = new TicketQuery { Search = "Contoso", Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Contoso Ltd", result.Items[0].Customer.Name);
    }

    [Fact]
    public async Task Search_matches_ticket_title()
    {
        await AddTicketAsync("Invoice discrepancy", _contoso.Id, _billing.Id, TicketPriority.High);
        await AddTicketAsync("Login issue", _fabrikam.Id, _general.Id, TicketPriority.Medium);

        var query = new TicketQuery { Search = "Invoice", Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Contains("Invoice", result.Items[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_matches_ticket_reference()
    {
        await AddTicketAsync("First ticket", _contoso.Id, _general.Id, TicketPriority.Low, "REF-0001");
        await AddTicketAsync("Second ticket", _fabrikam.Id, _general.Id, TicketPriority.Low, "REF-0002");

        var query = new TicketQuery { Search = "REF-0001", Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("REF-0001", result.Items[0].Reference);
    }

    [Fact]
    public async Task Blank_search_is_ignored_and_all_tickets_are_returned()
    {
        await AddTicketAsync("Ticket 1", _contoso.Id, _general.Id, TicketPriority.Medium);
        await AddTicketAsync("Ticket 2", _fabrikam.Id, _general.Id, TicketPriority.Medium);

        var query = new TicketQuery { Search = "   ", Page = 1, PageSize = 20 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
    }

    // -------------------------------------------------------------------------
    // FL-4 + FL-5: Combined filters; count is correct for the filtered set
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Combined_priority_and_customer_filters_apply_with_AND()
    {
        // Only one of these three meets both conditions: High priority AND Contoso.
        await AddTicketAsync("Contoso High", _contoso.Id, _general.Id, TicketPriority.High);
        await AddTicketAsync("Contoso Low", _contoso.Id, _general.Id, TicketPriority.Low);
        await AddTicketAsync("Fabrikam High", _fabrikam.Id, _general.Id, TicketPriority.High);

        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            CustomerId = _contoso.Id,
            Page = 1,
            PageSize = 20
        };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        // FL-5: totalCount reflects the filtered set (1), not all tickets (3).
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal("Contoso High", result.Items[0].Title);
    }

    [Fact]
    public async Task TotalCount_and_paging_reflect_filtered_set_not_all_tickets()
    {
        // 5 tickets; only 2 are Billing. With pageSize=1 we should see totalCount=2, totalPages=2.
        await AddTicketAsync("Billing 1", _contoso.Id, _billing.Id, TicketPriority.Medium);
        await AddTicketAsync("Billing 2", _fabrikam.Id, _billing.Id, TicketPriority.Medium);
        await AddTicketAsync("General 1", _contoso.Id, _general.Id, TicketPriority.Medium);
        await AddTicketAsync("General 2", _fabrikam.Id, _general.Id, TicketPriority.Medium);
        await AddTicketAsync("General 3", _contoso.Id, _general.Id, TicketPriority.Low);

        var query = new TicketQuery { CategoryId = _billing.Id, Page = 1, PageSize = 1 };

        var result = await _sut.GetPagedAsync(query.Normalized(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static int _refCounter;

    private async Task<Ticket> AddTicketAsync(
        string title,
        int customerId,
        int categoryId,
        TicketPriority priority,
        string? reference = null)
    {
        var counter = Interlocked.Increment(ref _refCounter);
        var ticket = Ticket.Raise(
            reference ?? $"TST-{counter:D4}",
            title,
            "A description long enough to be realistic.",
            customerId,
            categoryId,
            priority,
            FixedClock.DefaultNow);

        _db.Add(ticket);
        await _db.SaveChangesAsync();

        return ticket;
    }
}
