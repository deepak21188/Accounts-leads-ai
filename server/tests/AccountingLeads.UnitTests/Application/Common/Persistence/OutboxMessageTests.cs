using AccountingLeads.Application.Common.Persistence;

namespace AccountingLeads.UnitTests.Application.Common.Persistence;

/// <summary>
/// Unit tests for <see cref="OutboxMessage.Create"/>. <see cref="OutboxMessage"/> is a
/// persistence/infrastructure-shaped concept living in Application per
/// docs/architecture.md §17 — these tests only cover its own construction behavior, not any
/// EF Core mapping (there is none yet).
/// </summary>
public class OutboxMessageTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_GeneratesNonEmptyId()
    {
        var message = OutboxMessage.Create("LeadCreated", "{}", CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, message.Id);
    }

    [Fact]
    public void Create_CalledTwiceWithSameArguments_ProducesDifferentIds()
    {
        var first = OutboxMessage.Create("LeadCreated", "{}", CreatedAtUtc);
        var second = OutboxMessage.Create("LeadCreated", "{}", CreatedAtUtc);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_SetsTypePayloadAndCreatedAtUtcAsPassed()
    {
        var message = OutboxMessage.Create("LeadCreated", "{\"leadId\":\"abc\"}", CreatedAtUtc);

        Assert.Equal("LeadCreated", message.Type);
        Assert.Equal("{\"leadId\":\"abc\"}", message.Payload);
        Assert.Equal(CreatedAtUtc, message.CreatedAtUtc);
    }

    [Fact]
    public void Create_LeavesPublishedAtUtcNull()
    {
        var message = OutboxMessage.Create("LeadCreated", "{}", CreatedAtUtc);

        Assert.Null(message.PublishedAtUtc);
    }

    [Fact]
    public void MarkPublished_SetsPublishedAtUtcCloseToNow()
    {
        var message = OutboxMessage.Create("LeadCreated", "{}", CreatedAtUtc);

        var before = DateTime.UtcNow;
        message.MarkPublished();
        var after = DateTime.UtcNow;

        Assert.NotNull(message.PublishedAtUtc);
        Assert.InRange(message.PublishedAtUtc!.Value, before.AddSeconds(-5), after.AddSeconds(5));
    }

    [Fact]
    public void MarkPublished_CalledTwice_ThrowsInvalidOperationException()
    {
        var message = OutboxMessage.Create("LeadCreated", "{}", CreatedAtUtc);
        message.MarkPublished();

        Assert.Throws<InvalidOperationException>(() => message.MarkPublished());
    }

    [Fact]
    public void RecordFailedAttempt_IncrementsAttemptCountAndSetsFutureNextAttemptAtUtc()
    {
        var message = OutboxMessage.Create("LeadCreated", "{}", CreatedAtUtc);
        var attemptedAtUtc = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

        message.RecordFailedAttempt(attemptedAtUtc);

        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.NextAttemptAtUtc);
        Assert.True(message.NextAttemptAtUtc > attemptedAtUtc);
    }

    [Fact]
    public void RecordFailedAttempt_AfterPublished_ThrowsInvalidOperationException()
    {
        var message = OutboxMessage.Create("LeadCreated", "{}", CreatedAtUtc);
        message.MarkPublished();

        Assert.Throws<InvalidOperationException>(() => message.RecordFailedAttempt(DateTime.UtcNow));
    }
}
