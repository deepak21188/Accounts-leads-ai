using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.GetLeadDetail;

public sealed record LeadAnalysisDetail(
    IReadOnlyCollection<AccountingServiceType> RequestedServices,
    ClientType? ExtractedClientType,
    decimal? ExtractedApproximateAnnualRevenue,
    string? ExtractedAccountingSoftware,
    Urgency? Urgency,
    string? ExtractedFilingDeadline,
    string? Notes,
    int? EstimatedDeadlineInDays,
    int? BookkeepingMonthsBehind,
    DateTime CreatedAtUtc);
