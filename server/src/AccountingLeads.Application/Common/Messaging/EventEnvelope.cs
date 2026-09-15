namespace AccountingLeads.Application.Common.Messaging;

public sealed record EventEnvelope<TData>(
    Guid EventId,
    string EventType,
    int Version,
    DateTime OccurredAtUtc,
    TData Data);
