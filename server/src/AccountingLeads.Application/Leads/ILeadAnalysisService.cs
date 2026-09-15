using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads;

public interface ILeadAnalysisService
{
    Task<LeadAnalysisResult> AnalyzeAsync(Lead lead, CancellationToken cancellationToken);
}
