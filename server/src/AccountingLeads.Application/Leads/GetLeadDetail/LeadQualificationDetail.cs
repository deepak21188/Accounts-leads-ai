using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.GetLeadDetail;

public sealed record LeadQualificationDetail(
    int Score,
    LeadPriority Priority,
    IReadOnlyCollection<string> Reasons,
    string RulesVersionApplied,
    DateTime CreatedAtUtc);
