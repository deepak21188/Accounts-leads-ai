using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Api.Contracts;

public sealed record LeadQualificationResponse(
    int Score,
    LeadPriority Priority,
    IReadOnlyCollection<string> Reasons,
    string RulesVersionApplied,
    DateTime CreatedAtUtc);
