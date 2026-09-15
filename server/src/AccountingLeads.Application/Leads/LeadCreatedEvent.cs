namespace AccountingLeads.Application.Leads;

public sealed record LeadCreatedEvent(Guid LeadId)
{
    public const string EventType = "LeadCreated";
    public const int EventVersion = 1;
}
