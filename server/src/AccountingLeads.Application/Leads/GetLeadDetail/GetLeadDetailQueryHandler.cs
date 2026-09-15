using AccountingLeads.Application.Common.Interfaces;

namespace AccountingLeads.Application.Leads.GetLeadDetail;

public sealed class GetLeadDetailQueryHandler
{
    private readonly IApplicationDbContext _dbContext;

    public GetLeadDetailQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LeadDetailResult?> HandleAsync(GetLeadDetailQuery query, CancellationToken cancellationToken)
    {
        var lead = await _dbContext.GetLeadByIdAsync(query.LeadId, cancellationToken);
        if (lead is null)
        {
            return null;
        }

        var analysis = await _dbContext.GetLeadAnalysisByLeadIdAsync(query.LeadId, cancellationToken);
        var qualification = await _dbContext.GetLeadQualificationResultByLeadIdAsync(query.LeadId, cancellationToken);

        return new LeadDetailResult(
            lead.Id,
            lead.Name,
            lead.Email,
            lead.Phone,
            lead.CompanyName,
            lead.ClientType,
            lead.ApproximateAnnualRevenue,
            lead.AccountingSoftware,
            lead.Inquiry,
            lead.LeadStatus,
            lead.AiProcessingStatus,
            lead.CreatedAtUtc,
            analysis is null
                ? null
                : new LeadAnalysisDetail(
                    analysis.RequestedServices,
                    analysis.ExtractedClientType,
                    analysis.ExtractedApproximateAnnualRevenue,
                    analysis.ExtractedAccountingSoftware,
                    analysis.Urgency,
                    analysis.ExtractedFilingDeadline,
                    analysis.Notes,
                    analysis.EstimatedDeadlineInDays,
                    analysis.BookkeepingMonthsBehind,
                    analysis.CreatedAtUtc),
            qualification is null
                ? null
                : new LeadQualificationDetail(
                    qualification.Score,
                    qualification.Priority,
                    qualification.Reasons,
                    qualification.RulesVersionApplied,
                    qualification.CreatedAtUtc));
    }
}
