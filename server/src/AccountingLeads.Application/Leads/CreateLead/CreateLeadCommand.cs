using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.CreateLead;

public sealed record CreateLeadCommand(
    string Name,
    string Email,
    string? Phone,
    string? CompanyName,
    ClientType? ClientType,
    decimal? ApproximateAnnualRevenue,
    string? AccountingSoftware,
    string Inquiry);
