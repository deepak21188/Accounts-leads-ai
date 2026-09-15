using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Messaging;
using Microsoft.Extensions.Logging;

namespace AccountingLeads.Application.Outbox;

public sealed class OutboxPublicationService
{
    private const int BatchSize = 50;
    private const int AttemptCountLoggedAsError = 5;

    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxPublisher _publisher;
    private readonly ILogger<OutboxPublicationService> _logger;

    public OutboxPublicationService(
        IApplicationDbContext dbContext,
        IOutboxPublisher publisher,
        ILogger<OutboxPublicationService> logger)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var pending = await _dbContext.GetUnpublishedOutboxMessagesAsync(BatchSize, cancellationToken);

        var publishedCount = 0;
        foreach (var message in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
                message.MarkPublished();
                publishedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // This call's own token was cancelled: stop the batch, don't count it as a
                // failed send that should back off and retry.
                throw;
            }
            catch (Exception ex)
            {
                message.RecordFailedAttempt(DateTime.UtcNow);
                _logger.Log(
                    message.AttemptCount >= AttemptCountLoggedAsError ? LogLevel.Error : LogLevel.Warning,
                    ex,
                    "Failed to publish outbox message {OutboxMessageId} (attempt {AttemptCount}); will retry after {NextAttemptAtUtc:O}.",
                    message.Id,
                    message.AttemptCount,
                    message.NextAttemptAtUtc);
            }
        }

        // Save whenever anything was queried, not just on success — RecordFailedAttempt's
        // backoff bookkeeping must persist even when every message in the batch failed,
        // otherwise AttemptCount never advances and permanently-failing rows would block newer
        // ones forever.
        if (pending.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return publishedCount;
    }
}
