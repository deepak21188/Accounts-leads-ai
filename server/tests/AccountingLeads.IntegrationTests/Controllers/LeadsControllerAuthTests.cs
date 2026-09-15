using System.Net;
using System.Net.Http.Json;
using AccountingLeads.Api.Contracts;
using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Domain.Leads;
using AccountingLeads.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingLeads.IntegrationTests.Controllers;

/// <summary>
/// HTTP-level tests for the authentication/authorization boundary added in Slice 5
/// (docs/architecture.md §23): GET /api/leads requires a signed-in, correctly-scoped
/// accountant, while POST /api/leads stays anonymous. These use
/// <see cref="TestAuthenticationHandler"/> (see <see cref="TestAuthHeaders"/>) instead of real
/// Entra ID tokens.
///
/// Deliberately NOT covered here: wrong-tenant, wrong-audience, malformed, expired, and
/// ID-token-as-bearer scenarios. Those are real JWT signature/issuer/audience validation
/// concerns that a fake authentication scheme cannot exercise (it never parses a real token) —
/// they stay covered by the manual Entra verification checklist in docs/architecture.md §23,
/// not by tests in this file.
/// </summary>
public class LeadsControllerAuthTests : IClassFixture<IntegrationTestWebApplicationFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LeadsControllerAuthTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Shape of a <c>GET /api/leads</c> response body item used for deserialization here.
    /// <see cref="LeadListItemResponse"/>'s LeadStatus property is a Domain enum serialized as
    /// a string by the API's JsonStringEnumConverter; declaring it as a plain string here
    /// avoids needing to reconfigure the test HttpClient's JSON deserialization options to
    /// match (same reasoning as LeadsControllerTests.CreateLeadResponseBody).
    /// </summary>
    private sealed record LeadListItemBody(Guid Id, string Name, string? CompanyName, string LeadStatus, string AiProcessingStatus, string? Priority, DateTime CreatedAtUtc);

    private sealed record PagedLeadListBody(List<LeadListItemBody> Items, int TotalCount, int Page, int PageSize, int TotalPages);

    [Fact]
    public async Task GetLeads_WhenNoAuthHeaderPresent_Returns401()
    {
        var response = await _client.GetAsync("/api/leads");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLeads_WhenAuthenticatedWithoutRequiredScope_Returns403()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/leads");
        request.Headers.Add(TestAuthHeaders.Authenticated, "true");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLeads_WhenAuthenticatedWithRequiredScope_Returns200WithLeadList()
    {
        var lead = Lead.Create(
            name: "Jane Accountant",
            email: "jane@example.com",
            phone: null,
            companyName: "Jane's Bakery",
            clientType: null,
            approximateAnnualRevenue: null,
            accountingSoftware: null,
            inquiry: "I need help with catch-up bookkeeping for my small business.");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            dbContext.Leads.Add(lead);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/leads");
        request.Headers.Add(TestAuthHeaders.Authenticated, "true");
        request.Headers.Add(TestAuthHeaders.Scopes, "access_as_user");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedLeadListBody>();

        Assert.NotNull(body);
        Assert.Contains(body!.Items, item => item.Id == lead.Id && item.CompanyName == "Jane's Bakery");
    }

    [Fact]
    public async Task CreateLead_WithNoAuthHeader_StillReturns201()
    {
        var request = new CreateLeadRequest
        {
            Name = "Jane Accountant",
            Email = "jane@example.com",
            Inquiry = "I need help with catch-up bookkeeping for my small business.",
        };

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
