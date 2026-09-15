using System.Net;
using System.Net.Http.Json;
using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Domain.Leads;
using AccountingLeads.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingLeads.IntegrationTests.Controllers;

/// <summary>
/// HTTP-level tests for GET /api/leads/{id}, the Lead Detail endpoint backing the Accountant
/// Dashboard's failure-visibility requirement (docs/requirements.md §6.2): the API must not
/// fabricate placeholder analysis/qualification data — a lead's <c>AiProcessingStatus</c>
/// determines what the frontend should show, not whether nested objects happen to be present.
/// Uses <see cref="TestAuthenticationHandler"/> instead of real Entra ID tokens, same as
/// <see cref="LeadsControllerAuthTests"/>.
/// </summary>
public class LeadsControllerGetLeadDetailTests : IClassFixture<IntegrationTestWebApplicationFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LeadsControllerGetLeadDetailTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record LeadAnalysisBody(string[] RequestedServices, string? ExtractedClientType, string? Notes);

    private sealed record LeadQualificationBody(int Score, string Priority, string[] Reasons, string RulesVersionApplied);

    private sealed record LeadDetailBody(
        Guid Id, string Name, string Email, string Inquiry, string LeadStatus, string AiProcessingStatus,
        LeadAnalysisBody? Analysis, LeadQualificationBody? Qualification);

    private async Task<Lead> SeedLeadAsync()
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

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        dbContext.Leads.Add(lead);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return lead;
    }

    private async Task<HttpResponseMessage> GetLeadDetailAsync(Guid id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/leads/{id}");
        request.Headers.Add(TestAuthHeaders.Authenticated, "true");
        request.Headers.Add(TestAuthHeaders.Scopes, "access_as_user");

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task GetLeadDetail_WhenLeadDoesNotExist_Returns404()
    {
        var response = await GetLeadDetailAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLeadDetail_WhenAiProcessingIsPending_ReturnsNullAnalysisAndQualification()
    {
        var lead = await SeedLeadAsync();

        var response = await GetLeadDetailAsync(lead.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LeadDetailBody>();
        Assert.NotNull(body);
        Assert.Equal("Pending", body!.AiProcessingStatus);
        Assert.Null(body.Analysis);
        Assert.Null(body.Qualification);
    }

    [Fact]
    public async Task GetLeadDetail_WhenAiProcessingFailed_ReturnsNullAnalysis_NotFabricatedData()
    {
        Lead lead;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            lead = Lead.Create(
                name: "Jane Failed",
                email: "jane.failed@example.com",
                phone: null,
                companyName: null,
                clientType: null,
                approximateAnnualRevenue: null,
                accountingSoftware: null,
                inquiry: "I need help with catch-up bookkeeping for my small business.");
            lead.BeginAiProcessing();
            lead.FailAiProcessing();
            dbContext.Leads.Add(lead);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var response = await GetLeadDetailAsync(lead.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LeadDetailBody>();
        Assert.NotNull(body);
        Assert.Equal("Failed", body!.AiProcessingStatus);
        Assert.Null(body.Analysis);
        Assert.Null(body.Qualification);
    }

    [Fact]
    public async Task GetLeadDetail_WhenFullyProcessedAndQualified_ReturnsPopulatedAnalysisAndQualification()
    {
        Lead lead;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            lead = Lead.Create(
                name: "Jane Complete",
                email: "jane.complete@example.com",
                phone: null,
                companyName: "Jane's Bakery",
                clientType: null,
                approximateAnnualRevenue: null,
                accountingSoftware: null,
                inquiry: "I need help with catch-up bookkeeping for my small business.");
            lead.BeginAiProcessing();
            lead.CompleteAiProcessing();
            lead.MarkQualified();
            dbContext.Leads.Add(lead);

            var analysis = LeadAnalysis.Create(
                leadId: lead.Id,
                requestedServices: new[] { AccountingServiceType.CatchUpBookkeeping },
                extractedClientType: null,
                extractedApproximateAnnualRevenue: null,
                extractedAccountingSoftware: null,
                urgency: Urgency.High,
                extractedFilingDeadline: null,
                notes: "Client needs urgent bookkeeping catch-up.",
                estimatedDeadlineInDays: 14,
                bookkeepingMonthsBehind: 6);
            dbContext.LeadAnalyses.Add(analysis);

            var qualification = LeadQualificationResult.Create(lead.Id, 90, new[] { "High urgency." });
            dbContext.LeadQualificationResults.Add(qualification);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var response = await GetLeadDetailAsync(lead.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LeadDetailBody>();
        Assert.NotNull(body);
        Assert.Equal("Completed", body!.AiProcessingStatus);
        Assert.NotNull(body.Analysis);
        Assert.Equal("Client needs urgent bookkeeping catch-up.", body.Analysis!.Notes);
        Assert.NotNull(body.Qualification);
        Assert.Equal(90, body.Qualification!.Score);
        Assert.Equal("High", body.Qualification.Priority);
    }

    [Fact]
    public async Task GetLeadDetail_WhenNoAuthHeaderPresent_Returns401()
    {
        var lead = await SeedLeadAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/leads/{lead.Id}");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLeadDetail_WhenAuthenticatedWithoutRequiredScope_Returns403()
    {
        var lead = await SeedLeadAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/leads/{lead.Id}");
        request.Headers.Add(TestAuthHeaders.Authenticated, "true");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
