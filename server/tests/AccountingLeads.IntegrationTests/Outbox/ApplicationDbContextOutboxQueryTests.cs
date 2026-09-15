using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Infrastructure.Persistence;
using AccountingLeads.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingLeads.IntegrationTests.Outbox;

/// <summary>
/// Exercises the real EF Core/SQL translation of
/// <see cref="ApplicationDbContext.GetUnpublishedOutboxMessagesAsync"/> against the dedicated
/// LocalDB integration-test database. A hand-written in-memory fake's LINQ-to-Objects
/// filtering (used by <c>OutboxPublicationServiceTests</c> in AccountingLeads.UnitTests) proves
/// nothing about how EF Core actually translates this query to SQL — these tests are the
/// real check.
///
/// Each context comes from a fresh DI scope off <see cref="IntegrationTestWebApplicationFactory"/>
/// (the same mechanism <c>ResetDatabaseAsync</c> already uses, proven reliable by the rest of
/// this project's integration tests) rather than a manually-constructed
/// <see cref="ApplicationDbContext"/> — a scope gives an equally "fresh", untracked context
/// without duplicating connection-string/provider wiring outside the tested DI composition.
/// </summary>
public class ApplicationDbContextOutboxQueryTests : IClassFixture<IntegrationTestWebApplicationFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public ApplicationDbContextOutboxQueryTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private ApplicationDbContext CreateDbContext() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();

    [Fact]
    public async Task GetUnpublishedOutboxMessagesAsync_ExcludesPublishedRows()
    {
        var published = OutboxMessage.Create("LeadCreated", "{}", DateTime.UtcNow);
        published.MarkPublished();
        var unpublished = OutboxMessage.Create("LeadCreated", "{}", DateTime.UtcNow);

        await using (var seedContext = CreateDbContext())
        {
            seedContext.OutboxMessages.AddRange(published, unpublished);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = CreateDbContext();
        var result = await queryContext.GetUnpublishedOutboxMessagesAsync(50, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(unpublished.Id, result[0].Id);
    }

    [Fact]
    public async Task GetUnpublishedOutboxMessagesAsync_ExcludesRowsWithFutureNextAttemptAtUtc()
    {
        var notYetEligible = OutboxMessage.Create("LeadCreated", "{}", DateTime.UtcNow);
        notYetEligible.RecordFailedAttempt(DateTime.UtcNow);
        var eligible = OutboxMessage.Create("LeadCreated", "{}", DateTime.UtcNow);

        await using (var seedContext = CreateDbContext())
        {
            seedContext.OutboxMessages.AddRange(notYetEligible, eligible);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = CreateDbContext();
        var result = await queryContext.GetUnpublishedOutboxMessagesAsync(50, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(eligible.Id, result[0].Id);
    }

    [Fact]
    public async Task GetUnpublishedOutboxMessagesAsync_RespectsMaxCount()
    {
        var baseTime = DateTime.UtcNow;
        var messages = Enumerable.Range(0, 5)
            .Select(i => OutboxMessage.Create("LeadCreated", "{}", baseTime.AddSeconds(i)))
            .ToList();

        await using (var seedContext = CreateDbContext())
        {
            seedContext.OutboxMessages.AddRange(messages);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = CreateDbContext();
        var result = await queryContext.GetUnpublishedOutboxMessagesAsync(2, CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetUnpublishedOutboxMessagesAsync_OrdersByCreatedAtUtcThenId()
    {
        var baseTime = DateTime.UtcNow;
        var older = OutboxMessage.Create("LeadCreated", "{}", baseTime);
        var newer = OutboxMessage.Create("LeadCreated", "{}", baseTime.AddSeconds(1));
        // Two rows sharing the same CreatedAtUtc to prove the Id tie-breaker.
        var sameTimeFirst = OutboxMessage.Create("LeadCreated", "{}", baseTime.AddSeconds(2));
        var sameTimeSecond = OutboxMessage.Create("LeadCreated", "{}", baseTime.AddSeconds(2));

        await using (var seedContext = CreateDbContext())
        {
            seedContext.OutboxMessages.AddRange(newer, sameTimeSecond, older, sameTimeFirst);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var firstQueryContext = CreateDbContext();
        var firstResult = await firstQueryContext.GetUnpublishedOutboxMessagesAsync(50, CancellationToken.None);

        Assert.Equal(older.Id, firstResult[0].Id);
        Assert.Equal(newer.Id, firstResult[1].Id);
        Assert.Equal(
            new HashSet<Guid> { sameTimeFirst.Id, sameTimeSecond.Id },
            firstResult.Skip(2).Select(m => m.Id).ToHashSet());

        // SQL Server's uniqueidentifier byte-ordering doesn't match .NET's in-memory Guid
        // ordering, so "deterministic tie-breaker" is proven by repeating the query and
        // confirming the same order comes back both times — not by asserting a specific
        // expected order computed via LINQ-to-Objects.
        await using var secondQueryContext = CreateDbContext();
        var secondResult = await secondQueryContext.GetUnpublishedOutboxMessagesAsync(50, CancellationToken.None);

        Assert.Equal(firstResult.Select(m => m.Id), secondResult.Select(m => m.Id));
    }
}
