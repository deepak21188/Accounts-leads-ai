using System.Net;
using System.Net.Http.Json;
using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Domain.Leads;
using AccountingLeads.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingLeads.IntegrationTests.Controllers;

/// <summary>
/// HTTP-level tests for GET /api/leads's status/priority/search filters and server-side
/// pagination, added when the Accountant Dashboard's real Lead List replaced the bare
/// placeholder list. Uses <see cref="TestAuthenticationHandler"/> (see
/// <see cref="TestAuthHeaders"/>) instead of real Entra ID tokens, same as
/// <see cref="LeadsControllerAuthTests"/>.
/// </summary>
public class LeadsControllerFilterTests : IClassFixture<IntegrationTestWebApplicationFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LeadsControllerFilterTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record LeadListItemBody(Guid Id, string Name, string? CompanyName, string LeadStatus, string AiProcessingStatus, string? Priority, DateTime CreatedAtUtc);

    private sealed record PagedLeadListBody(List<LeadListItemBody> Items, int TotalCount, int Page, int PageSize, int TotalPages);

    private async Task<PagedLeadListBody> GetLeadsAsync(string queryString)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/leads{queryString}");
        request.Headers.Add(TestAuthHeaders.Authenticated, "true");
        request.Headers.Add(TestAuthHeaders.Scopes, "access_as_user");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedLeadListBody>();
        Assert.NotNull(body);
        return body!;
    }

    private async Task<Lead> SeedLeadAsync(string name, string email, string? companyName, int? qualificationScore = null)
    {
        var lead = Lead.Create(
            name: name,
            email: email,
            phone: null,
            companyName: companyName,
            clientType: null,
            approximateAnnualRevenue: null,
            accountingSoftware: null,
            inquiry: "I need help with catch-up bookkeeping for my small business.");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        dbContext.Leads.Add(lead);

        if (qualificationScore is { } score)
        {
            lead.MarkQualified();
            dbContext.LeadQualificationResults.Add(LeadQualificationResult.Create(lead.Id, score, new[] { "Test reason." }));
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);
        return lead;
    }

    [Fact]
    public async Task GetLeads_FilterByStatus_ReturnsOnlyMatchingLeads()
    {
        var newLead = await SeedLeadAsync("Jane New", "jane.new@example.com", null);
        var qualifiedLead = await SeedLeadAsync("Jane Qualified", "jane.qualified@example.com", null, qualificationScore: 90);

        var body = await GetLeadsAsync("?status=Qualified");

        Assert.Contains(body.Items, item => item.Id == qualifiedLead.Id);
        Assert.DoesNotContain(body.Items, item => item.Id == newLead.Id);
    }

    [Fact]
    public async Task GetLeads_FilterByPriority_ReturnsOnlyMatchingLeads()
    {
        var highPriorityLead = await SeedLeadAsync("Jane High", "jane.high@example.com", null, qualificationScore: 90);
        var lowPriorityLead = await SeedLeadAsync("Jane Low", "jane.low@example.com", null, qualificationScore: 10);

        var body = await GetLeadsAsync("?priority=High");

        Assert.Contains(body.Items, item => item.Id == highPriorityLead.Id);
        Assert.DoesNotContain(body.Items, item => item.Id == lowPriorityLead.Id);
    }

    [Fact]
    public async Task GetLeads_FilterBySearch_MatchesNameCompanyOrEmail()
    {
        var matchByCompany = await SeedLeadAsync("Someone", "someone@example.com", "Acme Bakery");
        var noMatch = await SeedLeadAsync("Nobody", "nobody@example.com", "Other Co");

        var body = await GetLeadsAsync("?search=Acme");

        Assert.Contains(body.Items, item => item.Id == matchByCompany.Id);
        Assert.DoesNotContain(body.Items, item => item.Id == noMatch.Id);
    }

    [Fact]
    public async Task GetLeads_CombinesFiltersWithAnd()
    {
        var matches = await SeedLeadAsync("Jane Match", "jane.match@example.com", "Acme Bakery", qualificationScore: 90);
        var wrongPriority = await SeedLeadAsync("Jane Wrong Priority", "jane.wrong@example.com", "Acme Bakery", qualificationScore: 10);
        var wrongSearch = await SeedLeadAsync("Someone Else", "someone.else@example.com", "Other Co", qualificationScore: 90);

        var body = await GetLeadsAsync("?priority=High&search=Acme");

        Assert.Contains(body.Items, item => item.Id == matches.Id);
        Assert.DoesNotContain(body.Items, item => item.Id == wrongPriority.Id);
        Assert.DoesNotContain(body.Items, item => item.Id == wrongSearch.Id);
    }

    [Fact]
    public async Task GetLeads_Pagination_ReturnsCorrectSliceAndTotals()
    {
        await SeedLeadAsync("Lead One", "one@example.com", null);
        await SeedLeadAsync("Lead Two", "two@example.com", null);
        await SeedLeadAsync("Lead Three", "three@example.com", null);

        var page1 = await GetLeadsAsync("?page=1&pageSize=2");
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(1, page1.Page);

        var page2 = await GetLeadsAsync("?page=2&pageSize=2");
        Assert.Single(page2.Items);
        Assert.Equal(3, page2.TotalCount);

        var idsAcrossPages = page1.Items.Select(i => i.Id).Concat(page2.Items.Select(i => i.Id)).Distinct();
        Assert.Equal(3, idsAcrossPages.Count());
    }

    [Fact]
    public async Task GetLeads_TotalCountReflectsFilteredSet_NotWholeTable()
    {
        await SeedLeadAsync("Jane New", "jane.new2@example.com", null);
        await SeedLeadAsync("Jane Qualified", "jane.qualified2@example.com", null, qualificationScore: 90);

        var body = await GetLeadsAsync("?status=Qualified");

        Assert.Equal(1, body.TotalCount);
    }

    [Fact]
    public async Task GetLeads_OutOfRangePage_ReturnsEmptyItemsNotError()
    {
        await SeedLeadAsync("Lead One", "one2@example.com", null);

        var body = await GetLeadsAsync("?page=999&pageSize=20");

        Assert.Empty(body.Items);
        Assert.Equal(1, body.TotalCount);
    }

    [Fact]
    public async Task GetLeads_SortByName_OrdersAscendingAndDescending()
    {
        var bravo = await SeedLeadAsync("Bravo Lead", "bravo@example.com", null);
        var alpha = await SeedLeadAsync("Alpha Lead", "alpha@example.com", null);

        var ascending = await GetLeadsAsync("?sortBy=name");
        var ascendingIds = ascending.Items.Select(i => i.Id).ToList();
        Assert.True(ascendingIds.IndexOf(alpha.Id) < ascendingIds.IndexOf(bravo.Id));

        var descending = await GetLeadsAsync("?sortBy=name&sortDescending=true");
        var descendingIds = descending.Items.Select(i => i.Id).ToList();
        Assert.True(descendingIds.IndexOf(bravo.Id) < descendingIds.IndexOf(alpha.Id));
    }

    [Fact]
    public async Task GetLeads_SortByName_UsesCompanyNameWhenPresent()
    {
        var withCompany = await SeedLeadAsync("Zed Person", "zed@example.com", "Alpha Company");
        var withoutCompany = await SeedLeadAsync("Beta Person", "beta@example.com", null);

        var ascending = await GetLeadsAsync("?sortBy=name");
        var ids = ascending.Items.Select(i => i.Id).ToList();

        // "Alpha Company" (withCompany's displayed value) sorts before "Beta Person" (withoutCompany's name).
        Assert.True(ids.IndexOf(withCompany.Id) < ids.IndexOf(withoutCompany.Id));
    }

    [Fact]
    public async Task GetLeads_SortByPriority_OrdersUnqualifiedLeadsConsistently()
    {
        var highPriority = await SeedLeadAsync("Jane High", "jane.sort.high@example.com", null, qualificationScore: 90);
        var unqualified = await SeedLeadAsync("Jane Unqualified", "jane.sort.none@example.com", null);

        var descending = await GetLeadsAsync("?sortBy=priority&sortDescending=true");
        var ids = descending.Items.Select(i => i.Id).ToList();

        Assert.True(ids.IndexOf(highPriority.Id) < ids.IndexOf(unqualified.Id));
    }

    [Fact]
    public async Task GetLeads_UnrecognizedSortBy_FallsBackToDefaultWithoutErroring()
    {
        await SeedLeadAsync("Lead One", "sortfallback1@example.com", null);
        await SeedLeadAsync("Lead Two", "sortfallback2@example.com", null);

        var body = await GetLeadsAsync("?sortBy=notARealColumn");

        Assert.Equal(2, body.Items.Count);
    }

    [Fact]
    public async Task GetLeads_SortIsStableAcrossPages_NoDuplicatesOrGaps()
    {
        // All three share the same LeadStatus (New), so sorting by status alone has ties —
        // the Id tie-breaker in the handler must still produce a stable, gap-free page split.
        var one = await SeedLeadAsync("Lead One", "stable1@example.com", null);
        var two = await SeedLeadAsync("Lead Two", "stable2@example.com", null);
        var three = await SeedLeadAsync("Lead Three", "stable3@example.com", null);

        var page1 = await GetLeadsAsync("?sortBy=status&page=1&pageSize=2");
        var page2 = await GetLeadsAsync("?sortBy=status&page=2&pageSize=2");

        var idsAcrossPages = page1.Items.Select(i => i.Id).Concat(page2.Items.Select(i => i.Id)).ToList();
        Assert.Equal(3, idsAcrossPages.Distinct().Count());
        Assert.Contains(one.Id, idsAcrossPages);
        Assert.Contains(two.Id, idsAcrossPages);
        Assert.Contains(three.Id, idsAcrossPages);
    }
}
