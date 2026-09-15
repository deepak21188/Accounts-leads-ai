using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Application.Leads.Qualification;

/// <summary>
/// Deterministic lead qualification per docs/qualification-rules-v1.md. Pure function of an
/// already-persisted <see cref="Lead"/> and its <see cref="LeadAnalysis"/> — no external
/// dependencies, no AI call, so unlike <see cref="ILeadAnalysisService"/> this needs no
/// interface or test double.
/// </summary>
public sealed class LeadQualificationService
{
    private const int NoSupportedServiceScoreCap = 39;

    /// <summary>
    /// Service Fit / Value points per docs/qualification-rules-v1.md §2.
    /// <see cref="AccountingServiceType.Other"/> is intentionally absent — it is the
    /// "unsupported" sentinel and is never a scoring key.
    /// </summary>
    private static readonly IReadOnlyDictionary<AccountingServiceType, int> ServicePoints = new Dictionary<AccountingServiceType, int>
    {
        [AccountingServiceType.CatchUpBookkeeping] = 25,
        [AccountingServiceType.CorporateTaxPreparation] = 20,
        [AccountingServiceType.Advisory] = 20,
        [AccountingServiceType.Bookkeeping] = 15,
        [AccountingServiceType.Payroll] = 15,
        [AccountingServiceType.AccountingSystemSetup] = 10,
        [AccountingServiceType.PersonalTaxPreparation] = 5
    };

    private static readonly IReadOnlyDictionary<AccountingServiceType, string> ServiceReasonText = new Dictionary<AccountingServiceType, string>
    {
        [AccountingServiceType.CatchUpBookkeeping] = "High-value catch-up bookkeeping service requested.",
        [AccountingServiceType.CorporateTaxPreparation] = "Corporate tax preparation requested.",
        [AccountingServiceType.Advisory] = "Advisory services requested.",
        [AccountingServiceType.Bookkeeping] = "Recurring bookkeeping requested.",
        [AccountingServiceType.Payroll] = "Payroll services requested.",
        [AccountingServiceType.AccountingSystemSetup] = "Accounting system setup requested.",
        [AccountingServiceType.PersonalTaxPreparation] = "Personal tax preparation requested."
    };

    public LeadQualificationResult Qualify(Lead lead, LeadAnalysis analysis)
    {
        var reasons = new List<string>();

        var (serviceScore, hasSupportedService) = ScoreServiceFit(analysis.RequestedServices, reasons);
        var revenueScore = ScoreRevenue(EffectiveRevenue(lead, analysis), reasons);
        var urgencyScore = ScoreUrgencyOrDeadline(analysis.EstimatedDeadlineInDays, analysis.Urgency, reasons);
        var workScopeScore = ScoreWorkScope(analysis.BookkeepingMonthsBehind, reasons);
        var completenessScore = ScoreCompleteness(lead, analysis, reasons);

        var score = Math.Clamp(serviceScore + revenueScore + urgencyScore + workScopeScore + completenessScore, 0, 100);

        if (!hasSupportedService)
        {
            score = Math.Min(score, NoSupportedServiceScoreCap);
            reasons.Add("No requested service is currently offered by the firm; qualification capped at Low priority.");
        }

        return LeadQualificationResult.Create(lead.Id, score, reasons);
    }

    // --- Dimension 1: Service Fit / Value (max 30) — qualification-rules-v1.md §2 ---
    private static (int Score, bool HasSupportedService) ScoreServiceFit(
        IReadOnlyCollection<AccountingServiceType> requestedServices, List<string> reasons)
    {
        var supported = requestedServices.Where(ServicePoints.ContainsKey).Distinct().ToList();

        if (supported.Count == 0)
        {
            return (0, false);
        }

        var topService = supported.OrderByDescending(service => ServicePoints[service]).First();
        var score = ServicePoints[topService];
        reasons.Add(ServiceReasonText[topService]);

        if (supported.Count >= 2)
        {
            score += 5;
            reasons.Add("Multiple supported accounting services requested.");
        }

        return (Math.Min(score, 30), true);
    }

    // --- Dimension 2: Annual Revenue (max 25) — qualification-rules-v1.md §3 ---
    private static int ScoreRevenue(decimal? effectiveRevenue, List<string> reasons)
    {
        if (effectiveRevenue is null)
        {
            return 0;
        }

        var revenue = effectiveRevenue.Value;

        if (revenue < 100_000m)
        {
            reasons.Add("Annual revenue is under $100,000.");
            return 5;
        }

        if (revenue < 250_000m)
        {
            reasons.Add("Annual revenue is between $100,000 and $249,999.");
            return 10;
        }

        if (revenue < 500_000m)
        {
            reasons.Add("Annual revenue is between $250,000 and $499,999.");
            return 15;
        }

        if (revenue < 1_000_000m)
        {
            reasons.Add("Annual revenue is between $500,000 and $999,999.");
            return 20;
        }

        reasons.Add("Annual revenue is $1,000,000 or more.");
        return 25;
    }

    // --- Dimension 3: Urgency / Deadline (max 25) — qualification-rules-v1.md §4 ---
    // An explicit deadline always takes precedence over the AI-extracted urgency label; the two
    // are never summed.
    private static int ScoreUrgencyOrDeadline(int? deadlineDays, Urgency? urgency, List<string> reasons)
    {
        if (deadlineDays is not null)
        {
            var days = deadlineDays.Value;

            if (days <= 7)
            {
                reasons.Add("Filing deadline is due or within 7 days.");
                return 25;
            }

            if (days <= 30)
            {
                reasons.Add("Filing deadline is within 30 days.");
                return 20;
            }

            if (days <= 60)
            {
                reasons.Add("Filing deadline is within 31 to 60 days.");
                return 10;
            }

            reasons.Add("Filing deadline is more than 60 days away.");
            return 5;
        }

        if (urgency == Urgency.High)
        {
            reasons.Add("Inquiry indicates high urgency.");
            return 15;
        }

        if (urgency == Urgency.Medium)
        {
            reasons.Add("Inquiry indicates medium urgency.");
            return 8;
        }

        return 0;
    }

    // --- Dimension 4: Catch-up Work / Scope (max 15) — qualification-rules-v1.md §5 ---
    private static int ScoreWorkScope(int? monthsBehind, List<string> reasons)
    {
        if (monthsBehind is null || monthsBehind <= 0)
        {
            return 0;
        }

        var months = monthsBehind.Value;

        if (months <= 2)
        {
            reasons.Add("Bookkeeping is one to two months behind.");
            return 5;
        }

        if (months <= 5)
        {
            reasons.Add("Bookkeeping is three to five months behind.");
            return 10;
        }

        reasons.Add("Bookkeeping is six or more months behind.");
        return 15;
    }

    // --- Dimension 5: Information Completeness (max 5) — qualification-rules-v1.md §6 ---
    private static int ScoreCompleteness(Lead lead, LeadAnalysis analysis, List<string> reasons)
    {
        var knownFacts = 0;

        if ((lead.ClientType ?? analysis.ExtractedClientType) is not null)
        {
            knownFacts++;
        }

        if (EffectiveRevenue(lead, analysis) is not null)
        {
            knownFacts++;
        }

        if (!string.IsNullOrWhiteSpace(lead.AccountingSoftware ?? analysis.ExtractedAccountingSoftware))
        {
            knownFacts++;
        }

        if (analysis.RequestedServices.Count > 0)
        {
            knownFacts++;
        }

        if (analysis.EstimatedDeadlineInDays is not null || analysis.Urgency is not null)
        {
            knownFacts++;
        }

        if (knownFacts >= 4)
        {
            reasons.Add("Inquiry contains sufficient information for follow-up.");
            return 5;
        }

        if (knownFacts >= 2)
        {
            reasons.Add("Inquiry contains some useful qualification information.");
            return 3;
        }

        return 0;
    }

    /// <summary>
    /// The client's own submitted revenue wins over the AI-extracted one — Slice 3's prompt
    /// deliberately leaves <see cref="LeadAnalysis.ExtractedApproximateAnnualRevenue"/> null when
    /// the client already supplied it on the form, so reading LeadAnalysis alone would silently
    /// zero-score those leads.
    /// </summary>
    private static decimal? EffectiveRevenue(Lead lead, LeadAnalysis analysis) =>
        lead.ApproximateAnnualRevenue ?? analysis.ExtractedApproximateAnnualRevenue;
}
