using AccountingLeads.Application.Leads;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.UnitTests.Application.TestDoubles;

/// <summary>
/// Hand-written <see cref="ILeadAnalysisService"/> test double. Returns a configurable
/// <see cref="LeadAnalysisResult"/> (defaulting to an empty-but-valid result) or throws a
/// configured exception, and records the lead passed to the most recent call.
/// </summary>
internal sealed class FakeLeadAnalysisService : ILeadAnalysisService
{
    private static readonly LeadAnalysisResult DefaultResult = new(
        RequestedServices: Array.Empty<AccountingServiceType>(),
        ExtractedClientType: null,
        ExtractedApproximateAnnualRevenue: null,
        ExtractedAccountingSoftware: null,
        Urgency: null,
        ExtractedFilingDeadline: null,
        Notes: null,
        EstimatedDeadlineInDays: null,
        BookkeepingMonthsBehind: null);

    public LeadAnalysisResult ResultToReturn { get; set; } = DefaultResult;

    public Exception? ExceptionToThrow { get; set; }

    public Lead? LastAnalyzedLead { get; private set; }

    public Task<LeadAnalysisResult> AnalyzeAsync(Lead lead, CancellationToken cancellationToken)
    {
        LastAnalyzedLead = lead;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(ResultToReturn);
    }
}
