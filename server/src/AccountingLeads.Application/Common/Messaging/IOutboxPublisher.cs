using AccountingLeads.Application.Common.Persistence;

namespace AccountingLeads.Application.Common.Messaging;

public interface IOutboxPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
