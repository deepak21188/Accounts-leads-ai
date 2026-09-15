using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Api.Contracts;

public sealed record LeadAnalysisResponse(
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
