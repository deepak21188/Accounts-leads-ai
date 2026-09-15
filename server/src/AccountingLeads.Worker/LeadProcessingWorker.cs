using System.Text.Json;
using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Leads;
using AccountingLeads.Application.Leads.ProcessLead;
using Azure.Messaging.ServiceBus;

namespace AccountingLeads.Worker;

/// <summary>
/// Consumes the <c>lead-created</c> queue via a push-based <see cref="ServiceBusProcessor"/>.
/// Implements <see cref="IHostedService"/> directly rather than <see cref="BackgroundService"/>:
/// the processor pumps messages on its own SDK-managed threads once started, so there's no loop
/// of this class's own to run — forcing it through <c>BackgroundService.ExecuteAsync</c> would
/// need an artificial <c>Task.Delay(Timeout.Infinite)</c> placeholder.
/// </summary>
public sealed class LeadProcessingWorker(
    ServiceBusProcessor processor,
    IServiceScopeFactory scopeFactory,
    ILogger<LeadProcessingWorker> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleErrorAsync;

        return processor.StartProcessingAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        processor.StopProcessingAsync(cancellationToken);

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var envelope = JsonSerializer.Deserialize<EventEnvelope<LeadCreatedEvent>>(args.Message.Body.ToString())
            ?? throw new InvalidOperationException($"Service Bus message {args.Message.MessageId} had an empty or invalid body.");

        // The lead-created queue is expected to carry only this one event contract. A
        // mismatched EventType/Version means either queue misuse or an incompatible future
        // producer — trust neither enough to run AI processing against Data.LeadId. Throwing
        // (rather than silently accepting) lets this dead-letter after 3 deliveries for
        // investigation, the same "genuine anomaly" handling ProcessLeadCommandHandler already
        // applies to a LeadId that doesn't exist.
        if (envelope.EventType != LeadCreatedEvent.EventType || envelope.Version != LeadCreatedEvent.EventVersion)
        {
            throw new InvalidOperationException(
                $"Service Bus message {args.Message.MessageId} has an unsupported event contract " +
                $"(EventType='{envelope.EventType}', Version={envelope.Version}); expected " +
                $"EventType='{LeadCreatedEvent.EventType}', Version={LeadCreatedEvent.EventVersion}.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessLeadCommandHandler>();

        // No manual complete/abandon: AutoCompleteMessages (the SDK default) completes the
        // message on a clean return here and leaves it uncompleted — available for redelivery —
        // if this throws, which is exactly the retry semantics this slice needs.
        await handler.HandleAsync(new ProcessLeadCommand(envelope.Data.LeadId), args.CancellationToken);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        // Never rethrow: an unhandled exception here would tear down the ServiceBusProcessor's
        // own error-handling pipeline, mirroring OutboxPublisherWorker's "don't crash the host"
        // philosophy for its own poll-loop exceptions.
        logger.LogError(args.Exception, "Service Bus error while processing leads (source: {ErrorSource}).", args.ErrorSource);

        return Task.CompletedTask;
    }
}
