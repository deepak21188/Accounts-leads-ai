using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.GetLeads;

public sealed record GetLeadsQuery(
    LeadStatus? Status,
    LeadPriority? Priority,
    string? Search,
    int Page,
    int PageSize,
    string? SortBy = null,
    bool SortDescending = false);
