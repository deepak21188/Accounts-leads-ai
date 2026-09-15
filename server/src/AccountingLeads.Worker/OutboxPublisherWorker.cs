using AccountingLeads.Application.Outbox;

namespace AccountingLeads.Worker;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var publicationService = scope.ServiceProvider.GetRequiredService<OutboxPublicationService>();
                var publishedCount = await publicationService.PublishPendingMessagesAsync(stoppingToken);

                if (publishedCount > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Published {Count} outbox message(s).", publishedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Real shutdown: let the host stop this service normally. Anything else,
                // including an unrelated OperationCanceledException, must stay caught below —
                // BackgroundServiceExceptionBehavior defaults to StopHost, so letting it escape
                // here would take down outbox publishing entirely until someone restarts the
                // process, not just skip this one tick.
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publication poll failed; will retry on the next tick.");
            }
        }
    }
}
