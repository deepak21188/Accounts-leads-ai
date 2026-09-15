namespace AccountingLeads.Application.Common.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }

    private OutboxMessage()
    {
        // Reserved for future EF Core materialization (Infrastructure slice).
    }

    public static OutboxMessage Create(string type, string payload, DateTime createdAtUtc) =>
        Create(Guid.NewGuid(), type, payload, createdAtUtc);

    // Lets a caller (e.g. CreateLeadCommandHandler) mint the id up front and embed it in the
    // payload itself (as an event id) before it's persisted here — so the outbox row's own Id,
    // the payload's event id, and the eventual Service Bus MessageId all stay the same value.
    public static OutboxMessage Create(Guid id, string type, string payload, DateTime createdAtUtc) =>
        new()
        {
            Id = id,
            Type = type,
            Payload = payload,
            CreatedAtUtc = createdAtUtc
        };

    public void MarkPublished()
    {
        if (PublishedAtUtc is not null)
        {
            throw new InvalidOperationException($"OutboxMessage {Id} has already been marked published.");
        }

        PublishedAtUtc = DateTime.UtcNow;
    }

    public void RecordFailedAttempt(DateTime attemptedAtUtc)
    {
        if (PublishedAtUtc is not null)
        {
            throw new InvalidOperationException($"OutboxMessage {Id} has already been marked published.");
        }

        AttemptCount++;
        var backoffSeconds = Math.Min(30 * Math.Pow(2, AttemptCount - 1), 1800);
        NextAttemptAtUtc = attemptedAtUtc + TimeSpan.FromSeconds(backoffSeconds);
    }
}
