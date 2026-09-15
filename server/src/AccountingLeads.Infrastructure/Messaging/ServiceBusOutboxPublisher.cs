using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Common.Persistence;
using Azure.Messaging.ServiceBus;

namespace AccountingLeads.Infrastructure.Messaging;

public sealed class ServiceBusOutboxPublisher : IOutboxPublisher
{
    // The SDK's own ServiceBusRetryOptions.TryTimeout bounds a single attempt, not the whole
    // operation — with MaxRetries retries the SDK could otherwise take well past this. This
    // linked-token timeout is an explicit, independent hard cap on the entire send (including
    // any SDK-internal retries), so one troublesome message can never stall a poll indefinitely.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

    private readonly ServiceBusSender _sender;

    public ServiceBusOutboxPublisher(ServiceBusSender sender)
    {
        _sender = sender;
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var serviceBusMessage = new ServiceBusMessage(message.Payload)
        {
            MessageId = message.Id.ToString(),
            Subject = message.Type,
            ContentType = "application/json"
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(SendTimeout);

        await _sender.SendMessageAsync(serviceBusMessage, timeoutCts.Token);
    }
}
