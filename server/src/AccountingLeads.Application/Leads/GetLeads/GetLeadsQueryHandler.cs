using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Models;
using AccountingLeads.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace AccountingLeads.Application.Leads.GetLeads;

public sealed class GetLeadsQueryHandler
{
    private readonly IApplicationDbContext _dbContext;

    public GetLeadsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<LeadSummary>> HandleAsync(GetLeadsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var filtered =
            from lead in _dbContext.Leads
            join qual in _dbContext.LeadQualificationResults on lead.Id equals qual.LeadId into quals
            from qual in quals.DefaultIfEmpty()
            select new { lead, qual };

        if (query.Status is { } status)
        {
            filtered = filtered.Where(x => x.lead.LeadStatus == status);
        }

        if (query.Priority is { } priority)
        {
            filtered = filtered.Where(x => x.qual != null && x.qual.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search;
            filtered = filtered.Where(x => x.lead.Name.Contains(term)
                || (x.lead.CompanyName != null && x.lead.CompanyName.Contains(term))
                || x.lead.Email.Contains(term));
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        // .ThenBy(lead.Id) breaks ties deterministically — without it, rows sharing a sort
        // value (e.g. the same LeadStatus) have no guaranteed stable order across the separate
        // count/page queries above, which can otherwise skip or duplicate rows across pages.
        var ordered = (query.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "name" => query.SortDescending
                ? filtered.OrderByDescending(x => x.lead.CompanyName ?? x.lead.Name).ThenBy(x => x.lead.Id)
                : filtered.OrderBy(x => x.lead.CompanyName ?? x.lead.Name).ThenBy(x => x.lead.Id),
            "status" => query.SortDescending
                ? filtered.OrderByDescending(x => x.lead.LeadStatus).ThenBy(x => x.lead.Id)
                : filtered.OrderBy(x => x.lead.LeadStatus).ThenBy(x => x.lead.Id),
            "aiprocessingstatus" => query.SortDescending
                ? filtered.OrderByDescending(x => x.lead.AiProcessingStatus).ThenBy(x => x.lead.Id)
                : filtered.OrderBy(x => x.lead.AiProcessingStatus).ThenBy(x => x.lead.Id),
            "priority" => query.SortDescending
                ? filtered.OrderByDescending(x => x.qual == null ? (LeadPriority?)null : x.qual.Priority).ThenBy(x => x.lead.Id)
                : filtered.OrderBy(x => x.qual == null ? (LeadPriority?)null : x.qual.Priority).ThenBy(x => x.lead.Id),
            "createdatutc" => query.SortDescending
                ? filtered.OrderByDescending(x => x.lead.CreatedAtUtc).ThenBy(x => x.lead.Id)
                : filtered.OrderBy(x => x.lead.CreatedAtUtc).ThenBy(x => x.lead.Id),
            _ => filtered.OrderByDescending(x => x.lead.CreatedAtUtc).ThenBy(x => x.lead.Id),
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LeadSummary(
                x.lead.Id,
                x.lead.Name,
                x.lead.CompanyName,
                x.lead.LeadStatus,
                x.lead.AiProcessingStatus,
                x.qual == null ? (LeadPriority?)null : x.qual.Priority,
                x.lead.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<LeadSummary>(items, totalCount, page, pageSize);
    }
}
