using System.Text.Json;
using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Domain.Exceptions;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.CreateLead;

public sealed class CreateLeadCommandHandler
{
    private readonly IApplicationDbContext _dbContext;

    public CreateLeadCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateLeadResult> HandleAsync(CreateLeadCommand command, CancellationToken cancellationToken)
    {
        Lead lead;
        try
        {
            lead = Lead.Create(
                command.Name,
                command.Email,
                command.Phone,
                command.CompanyName,
                command.ClientType,
                command.ApproximateAnnualRevenue,
                command.AccountingSoftware,
                command.Inquiry);
        }
        catch (DomainValidationException ex)
        {
            return CreateLeadResult.Failure(string.Empty, ex.Message);
        }

        var eventId = Guid.NewGuid();
        var envelope = new EventEnvelope<LeadCreatedEvent>(
            eventId,
            LeadCreatedEvent.EventType,
            LeadCreatedEvent.EventVersion,
            lead.CreatedAtUtc,
            new LeadCreatedEvent(lead.Id));
        var payload = JsonSerializer.Serialize(envelope);
        var outboxMessage = OutboxMessage.Create(eventId, LeadCreatedEvent.EventType, payload, lead.CreatedAtUtc);

        _dbContext.Leads.Add(lead);
        _dbContext.OutboxMessages.Add(outboxMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreateLeadResult.Success(lead.Id);
    }
}
