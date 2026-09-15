using System.Text.Json;
using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Leads;
using AccountingLeads.Application.Leads.CreateLead;
using AccountingLeads.Domain.Leads;
using AccountingLeads.UnitTests.Application.TestDoubles;

namespace AccountingLeads.UnitTests.Application.Leads.CreateLead;

/// <summary>
/// Unit tests for <see cref="CreateLeadCommandHandler"/>. Exercises the Application-layer
/// use case directly (no HTTP involved), per docs/architecture.md §12 — business validation
/// (Domain invariant violations surfaced as
/// <see cref="AccountingLeads.Domain.Exceptions.DomainValidationException"/>) is independent
/// of the API-level validation already covered by <c>CreateLeadRequestValidationTests</c>.
/// </summary>
public class CreateLeadCommandHandlerTests
{
    private static CreateLeadCommand ValidCommand(
        string name = "Jane Accountant",
        string email = "jane@example.com",
        string? phone = null,
        string? companyName = null,
        ClientType? clientType = null,
        decimal? approximateAnnualRevenue = null,
        string? accountingSoftware = null,
        string inquiry = "I need help with catch-up bookkeeping for my small business.") =>
        new(name, email, phone, companyName, clientType, approximateAnnualRevenue, accountingSoftware, inquiry);

    // ---- Invalid ClientType: fails before touching the DbContext -----------------

    [Fact]
    public async Task HandleAsync_WhenClientTypeIsUndefinedEnumValue_ReturnsFailureWithoutTouchingDbContext()
    {
        // (ClientType)99 does not correspond to any defined enum member. This bypasses API
        // model binding entirely (as e.g. the Worker or a future caller might), so it's
        // Lead.Create's own Enum.IsDefined guard that must catch it.
        var handler = new CreateLeadCommandHandler(new ThrowingApplicationDbContext());
        var command = ValidCommand(clientType: (ClientType)99);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.LeadId);
        Assert.True(result.ValidationErrors.ContainsKey(string.Empty));
        Assert.Equal("clientType is not a supported value.", result.ValidationErrors[string.Empty].Single());
    }

    // ---- Domain validation failures: fail before touching the DbContext ----------

    [Fact]
    public async Task HandleAsync_WhenNameIsMissing_ReturnsFailureWithoutTouchingDbContext()
    {
        var handler = new CreateLeadCommandHandler(new ThrowingApplicationDbContext());
        var command = ValidCommand(name: string.Empty);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.LeadId);
        Assert.True(result.ValidationErrors.ContainsKey(string.Empty));
        Assert.Equal("name is required.", result.ValidationErrors[string.Empty].Single());
    }

    [Fact]
    public async Task HandleAsync_WhenApproximateAnnualRevenueIsNegative_ReturnsFailureWithoutTouchingDbContext()
    {
        var handler = new CreateLeadCommandHandler(new ThrowingApplicationDbContext());
        var command = ValidCommand(approximateAnnualRevenue: -1m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.LeadId);
        Assert.True(result.ValidationErrors.ContainsKey(string.Empty));
        Assert.Equal("Approximate annual revenue cannot be negative.", result.ValidationErrors[string.Empty].Single());
    }

    [Fact]
    public async Task HandleAsync_WhenEmailIsMalformed_ReturnsFailureWithoutTouchingDbContext()
    {
        var handler = new CreateLeadCommandHandler(new ThrowingApplicationDbContext());
        var command = ValidCommand(email: "not-an-email");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.LeadId);
        Assert.True(result.ValidationErrors.ContainsKey(string.Empty));
    }

    // ---- Success path --------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WhenCommandIsValid_ReturnsSuccessWithLeadId()
    {
        var dbContext = new FakeApplicationDbContext();
        var handler = new CreateLeadCommandHandler(dbContext);
        var command = ValidCommand();

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.LeadId);
        Assert.NotEqual(Guid.Empty, result.LeadId!.Value);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public async Task HandleAsync_WhenCommandIsValid_AddsLeadToDbContext()
    {
        var dbContext = new FakeApplicationDbContext();
        var handler = new CreateLeadCommandHandler(dbContext);
        var command = ValidCommand(name: "Jane Accountant", email: "jane@example.com");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var addedLead = Assert.Single(dbContext.AddedLeads);
        Assert.Equal(result.LeadId, addedLead.Id);
        Assert.Equal("Jane Accountant", addedLead.Name);
        Assert.Equal("jane@example.com", addedLead.Email);
    }

    [Fact]
    public async Task HandleAsync_WhenCommandIsValid_AddsLeadCreatedOutboxMessageWithEnvelopeWrappingLeadId()
    {
        var dbContext = new FakeApplicationDbContext();
        var handler = new CreateLeadCommandHandler(dbContext);
        var command = ValidCommand();

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var outboxMessage = Assert.Single(dbContext.AddedOutboxMessages);
        Assert.Equal(LeadCreatedEvent.EventType, outboxMessage.Type);
        Assert.Null(outboxMessage.PublishedAtUtc);

        var envelope = JsonSerializer.Deserialize<EventEnvelope<LeadCreatedEvent>>(outboxMessage.Payload);
        Assert.NotNull(envelope);
        Assert.Equal(outboxMessage.Id, envelope!.EventId);
        Assert.Equal(LeadCreatedEvent.EventType, envelope.EventType);
        Assert.Equal(LeadCreatedEvent.EventVersion, envelope.Version);
        Assert.Equal(dbContext.AddedLeads.Single().CreatedAtUtc, envelope.OccurredAtUtc);
        Assert.Equal(result.LeadId, envelope.Data.LeadId);
    }

    [Fact]
    public async Task HandleAsync_WhenCommandIsValid_CallsSaveChangesAsyncExactlyOnce()
    {
        var dbContext = new FakeApplicationDbContext();
        var handler = new CreateLeadCommandHandler(dbContext);
        var command = ValidCommand();

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenClientTypeIsProvided_PassesClientTypeThroughToLead()
    {
        var dbContext = new FakeApplicationDbContext();
        var handler = new CreateLeadCommandHandler(dbContext);
        var command = ValidCommand(clientType: ClientType.Corporation);

        await handler.HandleAsync(command, CancellationToken.None);

        var addedLead = Assert.Single(dbContext.AddedLeads);
        Assert.Equal(ClientType.Corporation, addedLead.ClientType);
    }

    [Fact]
    public async Task HandleAsync_WhenClientTypeOmitted_LeadHasNullClientType()
    {
        var dbContext = new FakeApplicationDbContext();
        var handler = new CreateLeadCommandHandler(dbContext);
        var command = ValidCommand(clientType: null);

        await handler.HandleAsync(command, CancellationToken.None);

        var addedLead = Assert.Single(dbContext.AddedLeads);
        Assert.Null(addedLead.ClientType);
    }
}
