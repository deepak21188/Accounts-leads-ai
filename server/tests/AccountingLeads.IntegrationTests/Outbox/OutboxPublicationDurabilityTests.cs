using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Application.Outbox;
using AccountingLeads.Infrastructure.Persistence;
using AccountingLeads.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountingLeads.IntegrationTests.Outbox;

/// <summary>
/// Proves the at-least-once/duplicate-publication contract documented in
/// docs/architecture.md §18 end-to-end against real LocalDB persistence, not just in prose: if
/// a publish succeeds but the write that marks it published fails, a fresh polling scope must
/// still see the row as unpublished and resend it with the same MessageId.
///
/// This cannot be reproduced against the hand-written <c>FakeApplicationDbContext</c> used in
/// AccountingLeads.UnitTests — that fake mutates its tracked <see cref="OutboxMessage"/> the
/// instant <c>MarkPublished()</c> runs, regardless of whether "save" is later called or fails,
/// so a second call against the *same* fake would already see the row as published. A real
/// database, and a genuinely fresh <see cref="ApplicationDbContext"/> instance reading from it,
/// naturally reproduce "the write never committed" instead.
/// </summary>
public class OutboxPublicationDurabilityTests : IClassFixture<IntegrationTestWebApplicationFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public OutboxPublicationDurabilityTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private ApplicationDbContext CreateDbContext() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>
    /// Test-only decorator that delegates every member to a real <see cref="ApplicationDbContext"/>
    /// except <see cref="SaveChangesAsync"/>, which throws once (simulating "publish succeeded,
    /// but persisting the publication mark failed") and then delegates normally afterward.
    /// </summary>
    private sealed class SaveChangesFailsOnceDbContext : IApplicationDbContext
    {
        private readonly ApplicationDbContext _inner;
        private bool _shouldThrow = true;

        public SaveChangesFailsOnceDbContext(ApplicationDbContext inner) => _inner = inner;

        public DbSet<Domain.Leads.Lead> Leads => _inner.Leads;
        public DbSet<OutboxMessage> OutboxMessages => _inner.OutboxMessages;
        public DbSet<Domain.Leads.LeadAnalysis> LeadAnalyses => _inner.LeadAnalyses;

        public DbSet<Domain.Leads.LeadQualificationResult> LeadQualificationResults => _inner.LeadQualificationResults;

        public Task<IReadOnlyList<OutboxMessage>> GetUnpublishedOutboxMessagesAsync(int maxCount, CancellationToken cancellationToken) =>
            _inner.GetUnpublishedOutboxMessagesAsync(maxCount, cancellationToken);

        public Task<Domain.Leads.Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken) =>
            _inner.GetLeadByIdAsync(leadId, cancellationToken);

        public Task<Domain.Leads.LeadAnalysis?> GetLeadAnalysisByLeadIdAsync(Guid leadId, CancellationToken cancellationToken) =>
            _inner.GetLeadAnalysisByLeadIdAsync(leadId, cancellationToken);

        public Task<Domain.Leads.LeadQualificationResult?> GetLeadQualificationResultByLeadIdAsync(Guid leadId, CancellationToken cancellationToken) =>
            _inner.GetLeadQualificationResultByLeadIdAsync(leadId, cancellationToken);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (_shouldThrow)
            {
                _shouldThrow = false;
                throw new InvalidOperationException("Simulated SaveChanges failure for the duplicate-publication test.");
            }

            return _inner.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Minimal recording <see cref="IOutboxPublisher"/> double, local to this test class.
    /// Cannot reuse AccountingLeads.UnitTests' <c>FakeOutboxPublisher</c> — it is declared
    /// <c>internal</c> and not visible across assemblies.
    /// </summary>
    private sealed class RecordingOutboxPublisher : IOutboxPublisher
    {
        private readonly List<Guid> _publishedMessageIds = new();

        public IReadOnlyList<Guid> PublishedMessageIds => _publishedMessageIds;

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            _publishedMessageIds.Add(message.Id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishPendingMessagesAsync_WhenSaveAfterSendFails_NextPollResendsSameMessageId()
    {
        var outboxMessage = OutboxMessage.Create("LeadCreated", "{}", DateTime.UtcNow);

        await using (var seedContext = CreateDbContext())
        {
            seedContext.OutboxMessages.Add(outboxMessage);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var publisher = new RecordingOutboxPublisher();

        // First "poll": the send succeeds, but the save that would persist PublishedAtUtc fails.
        await using (var firstContext = CreateDbContext())
        {
            var wrappedContext = new SaveChangesFailsOnceDbContext(firstContext);
            var firstPollService = new OutboxPublicationService(
                wrappedContext, publisher, NullLogger<OutboxPublicationService>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => firstPollService.PublishPendingMessagesAsync(CancellationToken.None));
        }

        // Second "poll": a genuinely fresh ApplicationDbContext (no tracked entities), reading
        // straight from the database, which still shows the row as unpublished because the
        // first poll's save never committed.
        await using (var secondContext = CreateDbContext())
        {
            var secondPollService = new OutboxPublicationService(
                secondContext, publisher, NullLogger<OutboxPublicationService>.Instance);

            var publishedCount = await secondPollService.PublishPendingMessagesAsync(CancellationToken.None);

            Assert.Equal(1, publishedCount);
        }

        Assert.Equal(2, publisher.PublishedMessageIds.Count(id => id == outboxMessage.Id));

        await using var verifyContext = CreateDbContext();
        var persisted = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == outboxMessage.Id);
        Assert.NotNull(persisted.PublishedAtUtc);
    }
}
