using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Common.Persistence;

namespace AccountingLeads.UnitTests.Application.TestDoubles;

/// <summary>
/// Hand-written <see cref="IOutboxPublisher"/> test double. Records every message ID it was
/// asked to publish, in call order, and can be configured (per message ID) to throw a specific
/// exception and/or run a side-effect callback — the latter is used to simulate a real
/// cancellation mid-batch by having the callback call <see cref="CancellationTokenSource.Cancel()"/>
/// on a token source the test controls.
/// </summary>
internal sealed class FakeOutboxPublisher : IOutboxPublisher
{
    private readonly List<Guid> _publishedMessageIds = new();
    private readonly Dictionary<Guid, Exception> _exceptionsByMessageId = new();
    private readonly Dictionary<Guid, Action> _sideEffectsByMessageId = new();

    public IReadOnlyList<Guid> PublishedMessageIds => _publishedMessageIds;

    public void ThrowFor(Guid messageId, Exception exception) => _exceptionsByMessageId[messageId] = exception;

    public void InvokeOnPublish(Guid messageId, Action sideEffect) => _sideEffectsByMessageId[messageId] = sideEffect;

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _publishedMessageIds.Add(message.Id);

        if (_sideEffectsByMessageId.TryGetValue(message.Id, out var sideEffect))
        {
            sideEffect();
        }

        if (_exceptionsByMessageId.TryGetValue(message.Id, out var exception))
        {
            throw exception;
        }

        return Task.CompletedTask;
    }
}
