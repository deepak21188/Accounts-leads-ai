using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Api.Contracts;

public sealed record CreateLeadResponse(Guid Id, LeadStatus LeadStatus, AiProcessingStatus AiProcessingStatus);
