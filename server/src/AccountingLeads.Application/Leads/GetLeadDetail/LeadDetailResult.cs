using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.GetLeadDetail;

public sealed record LeadDetailResult(
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
    LeadAnalysisDetail? Analysis,
    LeadQualificationDetail? Qualification);
