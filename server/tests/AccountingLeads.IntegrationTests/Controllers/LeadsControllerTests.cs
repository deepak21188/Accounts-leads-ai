using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AccountingLeads.Api.Contracts;
using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Application.Leads;
using AccountingLeads.Domain.Leads;
using AccountingLeads.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingLeads.IntegrationTests.Controllers;

/// <summary>
/// End-to-end HTTP tests for POST /api/leads driven through the real ASP.NET Core pipeline
/// (routing, model binding, [ApiController] automatic validation, the real
/// CreateLeadCommandHandler, and a real SQL Server ApplicationDbContext against a dedicated
/// LocalDB integration-test database) via <see cref="IntegrationTestWebApplicationFactory"/>.
///
/// A structurally valid request now reaches the controller, is persisted, and receives
/// 201 Created — the Lead Capture vertical slice's Application/Domain/Infrastructure layers
/// are wired up end to end. The database is reset before every test (see
/// <see cref="InitializeAsync"/>) so tests do not leak state into one another, and this suite
/// only ever touches the dedicated AccountingLeadsDb_IntegrationTests database, never the
/// developer's personal AccountingLeadsDb.
/// </summary>
public class LeadsControllerTests : IClassFixture<IntegrationTestWebApplicationFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LeadsControllerTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static CreateLeadRequest ValidRequest() => new()
    {
        Name = "Jane Accountant",
        Email = "jane@example.com",
        Inquiry = "I need help with catch-up bookkeeping for my small business.",
    };

    /// <summary>
    /// Shape of the successful <c>POST /api/leads</c> response body used for deserialization
    /// in these tests. <see cref="CreateLeadResponse"/>'s LeadStatus/AiProcessingStatus
    /// properties are Domain enums serialized as strings by the API's JsonStringEnumConverter;
    /// declaring them as plain strings here avoids needing to reconfigure the test HttpClient's
    /// JSON deserialization options to match.
    /// </summary>
    private sealed record CreateLeadResponseBody(Guid Id, string LeadStatus, string AiProcessingStatus);

    private static async Task<CreateLeadResponseBody> AssertCreatedResponseAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateLeadResponseBody>();

        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal("New", body.LeadStatus);
        Assert.Equal("Pending", body.AiProcessingStatus);

        return body;
    }

    [Fact]
    public async Task CreateLead_WhenPayloadIsFullyValid_Returns201Created()
    {
        var response = await _client.PostAsJsonAsync("/api/leads", ValidRequest());

        await AssertCreatedResponseAsync(response);
    }

    [Fact]
    public async Task CreateLead_WhenOnlyMinimumRequiredFieldsProvided_Returns201Created()
    {
        // Name, Email, Inquiry only — all optional fields omitted.
        var request = ValidRequest();

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        await AssertCreatedResponseAsync(response);
    }

    [Fact]
    public async Task CreateLead_WhenPayloadIsFullyValid_PersistsLeadAndUnpublishedOutboxMessageAtomically()
    {
        var request = ValidRequest();

        var response = await _client.PostAsJsonAsync("/api/leads", request);
        var body = await AssertCreatedResponseAsync(response);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var persistedLead = await dbContext.Leads.SingleOrDefaultAsync(lead => lead.Id == body.Id);
        Assert.NotNull(persistedLead);
        Assert.Equal(request.Name, persistedLead!.Name);
        Assert.Equal(request.Email.ToLowerInvariant(), persistedLead.Email);
        Assert.Equal(request.Inquiry, persistedLead.Inquiry);
        Assert.Equal(LeadStatus.New, persistedLead.LeadStatus);
        Assert.Equal(AiProcessingStatus.Pending, persistedLead.AiProcessingStatus);

        var leadCreatedOutboxMessages = await dbContext.OutboxMessages
            .Where(message => message.Type == LeadCreatedEvent.EventType)
            .ToListAsync();

        var matchingOutboxMessage = Assert.Single(
            leadCreatedOutboxMessages,
            message => JsonSerializer.Deserialize<EventEnvelope<LeadCreatedEvent>>(message.Payload)!.Data.LeadId == body.Id);

        Assert.Null(matchingOutboxMessage.PublishedAtUtc);

        var envelope = JsonSerializer.Deserialize<EventEnvelope<LeadCreatedEvent>>(matchingOutboxMessage.Payload);
        Assert.NotNull(envelope);
        Assert.Equal(matchingOutboxMessage.Id, envelope!.EventId);
    }

    [Fact]
    public async Task SaveChanges_WhenOutboxMessageInsertFailsAtDatabaseLevel_RollsBackLeadInsertToo()
    {
        // Proves the failure-path half of the Transactional Outbox acceptance criterion
        // (docs/architecture.md §17, "Persistence failure does not leave one without the
        // other"): CreateLeadCommandHandler adds both a Lead and an OutboxMessage to the same
        // DbContext and calls SaveChangesAsync exactly once, so both inserts run inside a
        // single implicit database transaction. Because valid HTTP input can never produce an
        // OutboxMessage that fails to persist (CreateLeadCommandHandler always builds a small,
        // well-formed LeadCreated payload internally), this test bypasses the HTTP layer and
        // constructs the failure directly: a Lead that WOULD succeed on its own, paired with an
        // OutboxMessage whose Type is deliberately longer than the nvarchar(200) column mapped
        // by OutboxMessageConfiguration. OutboxMessage.Create performs no validation itself (it
        // is a thin persistence carrier, not a Domain entity), so construction succeeds and the
        // failure only surfaces when SQL Server rejects the INSERT. This still runs against the
        // real LocalDB engine, not a fake.
        var lead = Lead.Create(
            name: "Jane Accountant",
            email: "jane@example.com",
            phone: null,
            companyName: null,
            clientType: null,
            approximateAnnualRevenue: null,
            accountingSoftware: null,
            inquiry: "I need help with catch-up bookkeeping for my small business.");

        var oversizedType = new string('X', 300); // OutboxMessageConfiguration maps Type as nvarchar(200).
        var outboxMessage = OutboxMessage.Create(oversizedType, payload: "{}", createdAtUtc: DateTime.UtcNow);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            dbContext.Leads.Add(lead);
            dbContext.OutboxMessages.Add(outboxMessage);

            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(CancellationToken.None));
        }

        // Fresh scope/DbContext instance: the failed context's change tracker and connection
        // state must not be reused for the verification query.
        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var persistedLead = await verificationDbContext.Leads.SingleOrDefaultAsync(l => l.Id == lead.Id);
        Assert.Null(persistedLead);

        var persistedOutboxMessage = await verificationDbContext.OutboxMessages
            .SingleOrDefaultAsync(message => message.Id == outboxMessage.Id);
        Assert.Null(persistedOutboxMessage);
    }

    [Fact]
    public async Task CreateLead_WhenNameMissing_Returns400WithValidationErrorForName()
    {
        var request = ValidRequest();
        request.Name = string.Empty;

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.Name), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenEmailMissing_Returns400WithValidationErrorForEmail()
    {
        var request = ValidRequest();
        request.Email = string.Empty;

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.Email), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenInquiryMissing_Returns400WithValidationErrorForInquiry()
    {
        var request = ValidRequest();
        request.Inquiry = string.Empty;

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.Inquiry), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenEmailFormatIsInvalid_Returns400WithValidationErrorForEmail()
    {
        var request = ValidRequest();
        request.Email = "not-an-email";

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.Email), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenNameExceedsMaxLength_Returns400WithValidationErrorForName()
    {
        var request = ValidRequest();
        request.Name = new string('N', 151);

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.Name), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenInquiryExceedsMaxLength_Returns400WithValidationErrorForInquiry()
    {
        var request = ValidRequest();
        request.Inquiry = new string('I', 4001);

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.Inquiry), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenApproximateAnnualRevenueIsNegative_Returns400WithValidationError()
    {
        // Revenue has [Range(0, ...)] at the API layer, so a negative value is already
        // rejected structurally before ever reaching CreateLeadCommandHandler/Lead.Create.
        // This confirms it's unreachable as a distinct Application-level failure via valid
        // HTTP JSON; the corresponding Application-level rule is covered directly in
        // CreateLeadCommandHandlerTests (Application layer, no HTTP involved).
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = -1m;

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.ApproximateAnnualRevenue), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenEmailPassesApiValidationButFailsDomainValidation_Returns400WithValidationError()
    {
        // "test@localhost" satisfies [EmailAddress]'s permissive check (DataAnnotations only
        // requires a single '@' not in the first/last position), so it reaches
        // CreateLeadCommandHandler/Lead.Create. The domain's own email regex additionally
        // requires a dot in the domain part, so Lead.Create throws DomainValidationException
        // and the controller's manual ValidationProblem branch (LeadsController.CreateLead,
        // the `!result.IsSuccess` path) is what must convert that into 400 — distinct from the
        // automatic [ApiController] model-state validation exercised by the other 400 tests
        // in this class, and previously untested through the real HTTP pipeline.
        var request = ValidRequest();
        request.Email = "test@localhost";

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
    }

    [Fact]
    public async Task CreateLead_WhenApproximateAnnualRevenueIsZero_Returns201Created()
    {
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = 0m;

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        await AssertCreatedResponseAsync(response);
    }

    [Fact]
    public async Task CreateLead_WhenApproximateAnnualRevenueExceedsDecimalColumnCapacity_Returns400WithValidationError()
    {
        // Regression test for the bug where [Range] on ApproximateAnnualRevenue allowed values
        // up to decimal.MaxValue (~7.9e28) while Lead.Create only rejected negative values, but
        // the decimal(18,2) storage column (LeadConfiguration in Infrastructure) can only hold
        // up to 9999999999999999.99. 10_000_000_000_000_000 (1e16) is one cent over that ceiling
        // and is the exact repro value from the bug report: it used to pass both the API's
        // [Range] check and Lead.Create's negative-only check, reach CreateLeadCommandHandler,
        // and only fail deep inside SaveChangesAsync with an unhandled overflow exception —
        // surfacing as a 500, not a clean validation error. With the fix (Lead.MaxApproximateAnnualRevenue
        // enforced both in the [Range] attribute and in Lead.Create), the request is now
        // rejected at the API boundary before any Application/Infrastructure code runs.
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = 10_000_000_000_000_000m;

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.ApproximateAnnualRevenue), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenApproximateAnnualRevenueIsExactlyAtDecimalColumnCapacity_PersistsExactValue()
    {
        // Disproves the bug report's failure mode for the corrected boundary: the maximum
        // value the decimal(18,2) column can actually hold (9999999999999999.99) must be
        // accepted end to end and round-trip through SQL Server without truncation or overflow,
        // rather than crashing at SaveChangesAsync time as the pre-fix ceiling would have
        // allowed for values above this boundary.
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = Lead.MaxApproximateAnnualRevenue;

        var response = await _client.PostAsJsonAsync("/api/leads", request);
        var body = await AssertCreatedResponseAsync(response);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var persistedLead = await dbContext.Leads.SingleOrDefaultAsync(lead => lead.Id == body.Id);
        Assert.NotNull(persistedLead);
        Assert.Equal(Lead.MaxApproximateAnnualRevenue, persistedLead!.ApproximateAnnualRevenue);
    }

    [Fact]
    public async Task CreateLead_WhenMultipleFieldsInvalid_Returns400WithAllValidationErrors()
    {
        var request = new CreateLeadRequest
        {
            Name = string.Empty,
            Email = "not-an-email",
            Inquiry = string.Empty,
        };

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.Name), problem!.Errors.Keys);
        Assert.Contains(nameof(CreateLeadRequest.Email), problem.Errors.Keys);
        Assert.Contains(nameof(CreateLeadRequest.Inquiry), problem.Errors.Keys);
    }

    [Fact]
    public async Task CreateLead_WhenRequestBodyIsMalformedJson_Returns400()
    {
        using var content = new StringContent("{ this is not valid json", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/leads", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLead_WhenRequestBodyIsEmpty_Returns400()
    {
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/leads", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLead_WhenNameIsWhitespaceOnly_Returns400WithValidationErrorForName()
    {
        // RequiredAttribute trims strings, so a whitespace-only value must be rejected
        // the same way a missing value is — verified here through the real model-binding
        // pipeline, not just a direct Validator call.
        var request = ValidRequest();
        request.Name = "   ";

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.Name), problem!.Errors.Keys);
    }

    // ---- ClientType (optional enum, bound from JSON via JsonStringEnumConverter) --

    [Fact]
    public async Task CreateLead_WhenClientTypeOmitted_Returns201Created()
    {
        var request = ValidRequest();

        var response = await _client.PostAsJsonAsync("/api/leads", request);

        await AssertCreatedResponseAsync(response);
    }

    [Fact]
    public async Task CreateLead_WhenClientTypeIsValidEnumName_Returns201Created()
    {
        using var content = new StringContent(
            """
            {
                "name": "Jane Accountant",
                "email": "jane@example.com",
                "inquiry": "I need help with catch-up bookkeeping for my small business.",
                "clientType": "Corporation"
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/leads", content);

        await AssertCreatedResponseAsync(response);
    }

    [Fact]
    public async Task CreateLead_WhenClientTypeIsUnrecognizedString_Returns400()
    {
        using var content = new StringContent(
            """
            {
                "name": "Jane Accountant",
                "email": "jane@example.com",
                "inquiry": "I need help with catch-up bookkeeping for my small business.",
                "clientType": "NotARealType"
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/leads", content);

        // An unrecognized string fails JSON deserialization/model binding for the enum
        // property, which [ApiController] surfaces as 400 automatically. The exact response
        // shape/wording here is framework-generated and not asserted beyond the status code.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLead_WhenClientTypeIsOutOfRangeInteger_Returns400WithValidationErrorForClientType()
    {
        // JsonStringEnumConverter's default integer-acceptance path does not check
        // Enum.IsDefined, so 99 binds successfully to an undefined ClientType value. It's
        // [EnumDataType(typeof(ClientType))] on the request DTO, evaluated during normal
        // DataAnnotations validation after successful binding, that must catch this — before
        // it would otherwise reach CreateLeadCommandHandler/Lead.Create's own equivalent
        // guard (covered independently in CreateLeadCommandHandlerTests and LeadTests).
        using var content = new StringContent(
            """
            {
                "name": "Jane Accountant",
                "email": "jane@example.com",
                "inquiry": "I need help with catch-up bookkeeping for my small business.",
                "clientType": 99
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/leads", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLeadRequest.ClientType), problem!.Errors.Keys);
    }
}
