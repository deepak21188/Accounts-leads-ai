using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Application.Outbox;
using AccountingLeads.UnitTests.Application.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountingLeads.UnitTests.Application.Outbox;

/// <summary>
/// Unit tests for <see cref="OutboxPublicationService.PublishPendingMessagesAsync"/> against
/// hand-written fakes (<see cref="FakeApplicationDbContext"/>, <see cref="FakeOutboxPublisher"/>)
/// — no mocking framework, no real database/Service Bus involved. The duplicate-publication
/// scenario (send succeeds, save fails, a fresh scope resends the same MessageId) is
/// deliberately NOT tested here: a hand-written in-memory fake mutates its tracked
/// <see cref="OutboxMessage"/> the instant <c>MarkPublished()</c> runs regardless of whether a
/// later "save" succeeds, so it cannot reproduce "a fresh read sees the old state because the
/// write never committed" the way a real database naturally does. That scenario is covered
/// against real LocalDB in AccountingLeads.IntegrationTests instead.
/// </summary>
public class OutboxPublicationServiceTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    private static OutboxPublicationService CreateService(FakeApplicationDbContext dbContext, FakeOutboxPublisher publisher) =>
        new(dbContext, publisher, NullLogger<OutboxPublicationService>.Instance);

    private static OutboxMessage AddPendingMessage(FakeApplicationDbContext dbContext, DateTime createdAtUtc)
    {
        var message = OutboxMessage.Create("LeadCreated", "{}", createdAtUtc);
        dbContext.OutboxMessages.Add(message);
        return message;
    }

    [Fact]
    public async Task PublishPendingMessagesAsync_AllSendsSucceed_MarksAllPublishedAndSavesOnce()
    {
        var dbContext = new FakeApplicationDbContext();
        var first = AddPendingMessage(dbContext, CreatedAtUtc);
        var second = AddPendingMessage(dbContext, CreatedAtUtc.AddSeconds(1));
        var publisher = new FakeOutboxPublisher();
        var service = CreateService(dbContext, publisher);

        var publishedCount = await service.PublishPendingMessagesAsync(CancellationToken.None);

        Assert.Equal(2, publishedCount);
        Assert.NotNull(first.PublishedAtUtc);
        Assert.NotNull(second.PublishedAtUtc);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Equal([first.Id, second.Id], publisher.PublishedMessageIds);
    }

    [Fact]
    public async Task PublishPendingMessagesAsync_PartialFailure_OnlyMarksSuccessfulOnesPublished_AndRecordsAttemptOnFailedOnes()
    {
        var dbContext = new FakeApplicationDbContext();
        var succeeding = AddPendingMessage(dbContext, CreatedAtUtc);
        var failing = AddPendingMessage(dbContext, CreatedAtUtc.AddSeconds(1));
        var publisher = new FakeOutboxPublisher();
        publisher.ThrowFor(failing.Id, new InvalidOperationException("simulated send failure"));
        var service = CreateService(dbContext, publisher);

        var publishedCount = await service.PublishPendingMessagesAsync(CancellationToken.None);

        Assert.Equal(1, publishedCount);
        Assert.NotNull(succeeding.PublishedAtUtc);
        Assert.Null(failing.PublishedAtUtc);
        Assert.Equal(1, failing.AttemptCount);
        Assert.NotNull(failing.NextAttemptAtUtc);
    }

    [Fact]
    public async Task PublishPendingMessagesAsync_AllSendsFail_StillSavesRecordedAttempts()
    {
        var dbContext = new FakeApplicationDbContext();
        var first = AddPendingMessage(dbContext, CreatedAtUtc);
        var second = AddPendingMessage(dbContext, CreatedAtUtc.AddSeconds(1));
        var publisher = new FakeOutboxPublisher();
        publisher.ThrowFor(first.Id, new InvalidOperationException("simulated send failure"));
        publisher.ThrowFor(second.Id, new InvalidOperationException("simulated send failure"));
        var service = CreateService(dbContext, publisher);

        var publishedCount = await service.PublishPendingMessagesAsync(CancellationToken.None);

        Assert.Equal(0, publishedCount);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Equal(1, first.AttemptCount);
        Assert.Equal(1, second.AttemptCount);
    }

    [Fact]
    public async Task PublishPendingMessagesAsync_CancellationDuringBatch_StopsProcessingRemainingMessages()
    {
        var dbContext = new FakeApplicationDbContext();
        var first = AddPendingMessage(dbContext, CreatedAtUtc);
        var second = AddPendingMessage(dbContext, CreatedAtUtc.AddSeconds(1));
        var third = AddPendingMessage(dbContext, CreatedAtUtc.AddSeconds(2));
        var publisher = new FakeOutboxPublisher();
        using var cts = new CancellationTokenSource();
        publisher.InvokeOnPublish(second.Id, cts.Cancel);
        var service = CreateService(dbContext, publisher);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.PublishPendingMessagesAsync(cts.Token));

        Assert.Equal([first.Id, second.Id], publisher.PublishedMessageIds);
        Assert.DoesNotContain(third.Id, publisher.PublishedMessageIds);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }
}
