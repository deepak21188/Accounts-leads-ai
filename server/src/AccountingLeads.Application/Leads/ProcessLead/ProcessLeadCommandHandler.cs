using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Leads.Qualification;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.ProcessLead;

public sealed class ProcessLeadCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILeadAnalysisService _analysisService;
    private readonly LeadQualificationService _qualificationService;

    public ProcessLeadCommandHandler(
        IApplicationDbContext dbContext,
        ILeadAnalysisService analysisService,
        LeadQualificationService qualificationService)
    {
        _dbContext = dbContext;
        _analysisService = analysisService;
        _qualificationService = qualificationService;
    }

    public async Task<ProcessLeadResult> HandleAsync(ProcessLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await _dbContext.GetLeadByIdAsync(command.LeadId, cancellationToken)
            ?? throw new InvalidOperationException($"Lead {command.LeadId} was not found.");

        if (lead.AiProcessingStatus == AiProcessingStatus.Completed)
        {
            if (lead.LeadStatus == LeadStatus.Qualified)
            {
                return ProcessLeadResult.AlreadyCompleted();
            }

            // AI analysis already completed — most likely a lead processed before qualification
            // existed (Slice 3, pre-Slice 4) — but it was never qualified. Qualify its existing
            // analysis without calling AI again, rather than leaving it permanently unscored.
            var existingAnalysis = await _dbContext.GetLeadAnalysisByLeadIdAsync(lead.Id, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Lead {lead.Id} has AiProcessingStatus.Completed but no LeadAnalysis was found.");

            var backfilledQualification = _qualificationService.Qualify(lead, existingAnalysis);
            _dbContext.LeadQualificationResults.Add(backfilledQualification);
            lead.MarkQualified();

            await _dbContext.SaveChangesAsync(cancellationToken);

            return ProcessLeadResult.QualifiedFromExistingAnalysis();
        }

        lead.BeginAiProcessing();

        try
        {
            var analysisResult = await _analysisService.AnalyzeAsync(lead, cancellationToken);

            var analysis = LeadAnalysis.Create(
                lead.Id,
                analysisResult.RequestedServices,
                analysisResult.ExtractedClientType,
                analysisResult.ExtractedApproximateAnnualRevenue,
                analysisResult.ExtractedAccountingSoftware,
                analysisResult.Urgency,
                analysisResult.ExtractedFilingDeadline,
                analysisResult.Notes,
                analysisResult.EstimatedDeadlineInDays,
                analysisResult.BookkeepingMonthsBehind);

            _dbContext.LeadAnalyses.Add(analysis);

            var qualification = _qualificationService.Qualify(lead, analysis);
            _dbContext.LeadQualificationResults.Add(qualification);
            lead.MarkQualified();

            lead.CompleteAiProcessing();
        }
        catch (Exception)
        {
            // Covers both a failing AnalyzeAsync call and an AI response that fails
            // LeadAnalysis.Create's own Domain validation (e.g. a revenue figure or Notes
            // string outside bounds) — either way the lead must not be left without a recorded
            // outcome. Must persist the failure before rethrowing: the DbContext is scoped to
            // this message and disposed when the caller's scope ends, so skipping this save
            // would silently lose the Failed status even though the exception still propagates
            // to abandon the Service Bus message for redelivery.
            lead.FailAiProcessing();
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ProcessLeadResult.Processed();
    }
}
