using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads;

public sealed record LeadAnalysisResult(
    IReadOnlyCollection<AccountingServiceType> RequestedServices,
    ClientType? ExtractedClientType,
    decimal? ExtractedApproximateAnnualRevenue,
    string? ExtractedAccountingSoftware,
    Urgency? Urgency,
    string? ExtractedFilingDeadline,
    string? Notes,
    int? EstimatedDeadlineInDays,
    int? BookkeepingMonthsBehind);
