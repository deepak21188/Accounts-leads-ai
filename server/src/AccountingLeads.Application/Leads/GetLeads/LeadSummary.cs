using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.GetLeads;

public sealed record LeadSummary(
    Guid Id,
    string Name,
    string? CompanyName,
    LeadStatus LeadStatus,
    AiProcessingStatus AiProcessingStatus,
    LeadPriority? Priority,
    DateTime CreatedAtUtc);
