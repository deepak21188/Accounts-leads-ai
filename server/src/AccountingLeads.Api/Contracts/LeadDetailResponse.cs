using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Api.Contracts;

public sealed record LeadDetailResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string? CompanyName,
    ClientType? ClientType,
    decimal? ApproximateAnnualRevenue,
    string? AccountingSoftware,
    string Inquiry,
    LeadStatus LeadStatus,
    AiProcessingStatus AiProcessingStatus,
    DateTime CreatedAtUtc,
    LeadAnalysisResponse? Analysis,
    LeadQualificationResponse? Qualification);
