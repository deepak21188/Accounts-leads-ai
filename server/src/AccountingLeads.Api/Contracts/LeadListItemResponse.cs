using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Api.Contracts;

public sealed record LeadListItemResponse(
    Guid Id,
    string Name,
    string? CompanyName,
    LeadStatus LeadStatus,
    AiProcessingStatus AiProcessingStatus,
    LeadPriority? Priority,
    DateTime CreatedAtUtc);
