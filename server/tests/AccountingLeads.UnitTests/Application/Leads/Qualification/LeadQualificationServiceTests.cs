using AccountingLeads.Application.Leads.Qualification;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.UnitTests.Application.Leads.Qualification;

/// <summary>
/// Unit tests for <see cref="LeadQualificationService"/> against docs/qualification-rules-v1.md.
/// No mocking library, per this codebase's convention — <see cref="LeadQualificationService"/>
/// has no external dependencies, so tests call it directly against hand-built <see cref="Lead"/>/
/// <see cref="LeadAnalysis"/> fixtures.
///
/// Most boundary tests isolate a single dimension by keeping every other input at a
/// zero-contribution default (no requested services, no revenue, no urgency/deadline, no
/// months-behind, no client type/software) so the total <see cref="LeadQualificationResult.Score"/>
/// equals exactly the dimension under test — see each region's fixture for what "zero-contribution
/// default" means for that dimension.
/// </summary>
public class LeadQualificationServiceTests
{
    private static readonly LeadQualificationService Service = new();

    private static Lead CreateLead(
        ClientType? clientType = null,
        decimal? approximateAnnualRevenue = null,
        string? accountingSoftware = null) =>
        Lead.Create(
            "Jane Prospect",
            "jane@example.com",
            phone: null,
            companyName: null,
            clientType: clientType,
            approximateAnnualRevenue: approximateAnnualRevenue,
            accountingSoftware: accountingSoftware,
            inquiry: "Test inquiry for qualification scoring.");

    private static LeadAnalysis CreateAnalysis(
        Guid? leadId = null,
        IReadOnlyCollection<AccountingServiceType>? requestedServices = null,
        ClientType? extractedClientType = null,
        decimal? extractedApproximateAnnualRevenue = null,
        string? extractedAccountingSoftware = null,
        Urgency? urgency = null,
        int? estimatedDeadlineInDays = null,
        int? bookkeepingMonthsBehind = null) =>
        LeadAnalysis.Create(
            leadId ?? Guid.NewGuid(),
            requestedServices ?? Array.Empty<AccountingServiceType>(),
            extractedClientType,
            extractedApproximateAnnualRevenue,
            extractedAccountingSoftware,
            urgency,
            extractedFilingDeadline: null,
            notes: null,
            estimatedDeadlineInDays,
            bookkeepingMonthsBehind);

    // ---- Service Fit / Value (max 30) — qualification-rules-v1.md §2 -----------------------

    [Theory]
    [InlineData(AccountingServiceType.CatchUpBookkeeping, 25)]
    [InlineData(AccountingServiceType.CorporateTaxPreparation, 20)]
    [InlineData(AccountingServiceType.Advisory, 20)]
    [InlineData(AccountingServiceType.Bookkeeping, 15)]
    [InlineData(AccountingServiceType.Payroll, 15)]
    [InlineData(AccountingServiceType.AccountingSystemSetup, 10)]
    [InlineData(AccountingServiceType.PersonalTaxPreparation, 5)]
    public void Qualify_WithSingleSupportedService_AwardsItsDocumentedPoints(
        AccountingServiceType serviceType, int expectedPoints)
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(requestedServices: new[] { serviceType });

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(expectedPoints, result.Score);
    }

    [Fact]
    public void Qualify_WhenOnlyUnsupportedServiceRequested_AwardsZeroServicePointsAndCaps()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(requestedServices: new[] { AccountingServiceType.Other });

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(0, result.Score);
        Assert.Equal(LeadPriority.Low, result.Priority);
    }

    [Fact]
    public void Qualify_WhenNoServicesRequestedAtAll_TreatsAsUnsupportedAndCaps()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(requestedServices: Array.Empty<AccountingServiceType>());

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(0, result.Score);
        Assert.Equal(LeadPriority.Low, result.Priority);
    }

    [Fact]
    public void Qualify_WhenTwoOrMoreSupportedServicesRequested_AddsFiveBonusOnTopOfHighestOnly()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(requestedServices: new[]
        {
            AccountingServiceType.CatchUpBookkeeping,
            AccountingServiceType.PersonalTaxPreparation
        });

        var result = Service.Qualify(lead, analysis);

        // 25 (CatchUpBookkeeping) + 5 (multi-service bonus) = 30, NOT 25 + 5 + 5.
        Assert.Equal(30, result.Score);
    }

    [Fact]
    public void Qualify_WhenTwoHighestValueServicesRequested_ScoreNeverExceedsThirty()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(requestedServices: new[]
        {
            AccountingServiceType.CatchUpBookkeeping,
            AccountingServiceType.CorporateTaxPreparation
        });

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(30, result.Score);
    }

    [Fact]
    public void Qualify_WhenSupportedServiceMixedWithOther_IgnoresOtherAndDoesNotCap()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(requestedServices: new[]
        {
            AccountingServiceType.Bookkeeping,
            AccountingServiceType.Other
        });

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(15, result.Score);
        Assert.DoesNotContain(result.Reasons, r => r.Contains("No requested service"));
    }

    [Fact]
    public void Qualify_WhenUnsupportedServiceCombinedWithHighRevenueAndUrgentDeadline_CapsScoreAt39AndForcesLow()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(
            requestedServices: new[] { AccountingServiceType.Other },
            extractedApproximateAnnualRevenue: 1_000_000m,
            estimatedDeadlineInDays: 5);

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(39, result.Score);
        Assert.Equal(LeadPriority.Low, result.Priority);
        Assert.Contains(result.Reasons, r => r.Contains("No requested service"));
    }

    // ---- Annual Revenue (max 25) — qualification-rules-v1.md §3 -----------------------------

    [Theory]
    [InlineData(99_999, 5)]
    [InlineData(100_000, 10)]
    [InlineData(249_999, 10)]
    [InlineData(250_000, 15)]
    [InlineData(499_999, 15)]
    [InlineData(500_000, 20)]
    [InlineData(999_999, 20)]
    [InlineData(1_000_000, 25)]
    public void Qualify_RevenueBoundaries_AwardDocumentedPoints(decimal revenue, int expectedPoints)
    {
        var lead = CreateLead(approximateAnnualRevenue: revenue);
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(expectedPoints, result.Score);
    }

    [Fact]
    public void Qualify_WhenRevenueIsUnknown_AwardsZeroRevenuePoints()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(0, result.Score);
    }

    // ---- Coalescing: Lead's own submitted value wins over LeadAnalysis's extracted one -------

    [Fact]
    public void Qualify_WhenOnlyLeadHasRevenue_UsesLeadRevenue()
    {
        var lead = CreateLead(approximateAnnualRevenue: 700_000m);
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(20, result.Score); // $500,000-$999,999 bucket
    }

    [Fact]
    public void Qualify_WhenOnlyAnalysisHasExtractedRevenue_UsesAnalysisRevenue()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(extractedApproximateAnnualRevenue: 700_000m);

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(20, result.Score); // $500,000-$999,999 bucket
    }

    [Fact]
    public void Qualify_WhenBothLeadAndAnalysisHaveRevenue_PrefersLeadsOwnSubmittedValue()
    {
        var lead = CreateLead(approximateAnnualRevenue: 700_000m); // -> 20 points
        var analysis = CreateAnalysis(extractedApproximateAnnualRevenue: 50_000m); // -> would be 5 points

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(20, result.Score);
    }

    // ---- Urgency / Deadline (max 25) — qualification-rules-v1.md §4 -------------------------

    [Theory]
    [InlineData(-3, 25)] // overdue deadline — still the highest urgency band, not an error
    [InlineData(0, 25)]
    [InlineData(7, 25)]
    [InlineData(8, 20)]
    [InlineData(30, 20)]
    [InlineData(31, 10)]
    [InlineData(60, 10)]
    [InlineData(61, 5)]
    public void Qualify_DeadlineBoundaries_AwardDocumentedPoints(int deadlineDays, int expectedPoints)
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(estimatedDeadlineInDays: deadlineDays);

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(expectedPoints, result.Score);
    }

    [Theory]
    [InlineData(Urgency.High, 15)]
    [InlineData(Urgency.Medium, 8)]
    [InlineData(Urgency.Low, 0)]
    public void Qualify_WhenNoExplicitDeadline_FallsBackToUrgency(Urgency urgency, int expectedPoints)
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(urgency: urgency);

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(expectedPoints, result.Score);
    }

    [Fact]
    public void Qualify_WhenNeitherDeadlineNorUrgencyKnown_AwardsZeroPoints()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Qualify_WhenBothExplicitDeadlineAndExtractedUrgencyPresent_UsesDeadlineOnlyNotBoth()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(estimatedDeadlineInDays: 10, urgency: Urgency.High);

        var result = Service.Qualify(lead, analysis);

        // Deadline (10 days -> 20) must win outright, never 20 + 15 = 35.
        Assert.Equal(20, result.Score);
    }

    // ---- Catch-up Work / Scope (max 15) — qualification-rules-v1.md §5 ----------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 5)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(5, 10)]
    [InlineData(6, 15)]
    public void Qualify_MonthsBehindBoundaries_AwardDocumentedPoints(int monthsBehind, int expectedPoints)
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis(bookkeepingMonthsBehind: monthsBehind);

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(expectedPoints, result.Score);
    }

    [Fact]
    public void Qualify_WhenMonthsBehindIsUnknown_AwardsZeroPoints()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(0, result.Score);
    }

    // ---- Information Completeness (max 5) — qualification-rules-v1.md §6 --------------------
    // ClientType, AccountingSoftware, an unsupported-only RequestedServices entry, and
    // Urgency.Low each count toward "known facts" without contributing to any other dimension's
    // score, so they can be combined freely to hit an exact known-fact count.

    [Fact]
    public void Qualify_WithZeroKnownFacts_AwardsZeroCompletenessPoints()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Qualify_WithOneKnownFact_AwardsZeroCompletenessPoints()
    {
        var lead = CreateLead(clientType: ClientType.Corporation);
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Qualify_WithTwoKnownFacts_AwardsThreeCompletenessPoints()
    {
        var lead = CreateLead(clientType: ClientType.Corporation, accountingSoftware: "Xero");
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(3, result.Score);
    }

    [Fact]
    public void Qualify_WithThreeKnownFacts_AwardsThreeCompletenessPoints()
    {
        var lead = CreateLead(clientType: ClientType.Corporation, accountingSoftware: "Xero");
        var analysis = CreateAnalysis(urgency: Urgency.Low);

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(3, result.Score);
    }

    [Fact]
    public void Qualify_WithFourKnownFacts_AwardsFiveCompletenessPoints()
    {
        var lead = CreateLead(clientType: ClientType.Corporation, accountingSoftware: "Xero");
        var analysis = CreateAnalysis(
            requestedServices: new[] { AccountingServiceType.Other },
            urgency: Urgency.Low);

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(5, result.Score);
    }

    // ---- Final score / clamp / determinism ---------------------------------------------------

    [Fact]
    public void Qualify_WhenAllOptionalInformationIsMissing_StillSucceedsWithLowScore()
    {
        var lead = CreateLead();
        var analysis = CreateAnalysis();

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(0, result.Score);
        Assert.Equal(LeadPriority.Low, result.Priority);
    }

    [Fact]
    public void Qualify_WhenEveryDimensionIsAtItsMaximum_ScoreIsExactlyOneHundred()
    {
        var lead = CreateLead(clientType: ClientType.Corporation, accountingSoftware: "QuickBooks");
        var analysis = CreateAnalysis(
            requestedServices: new[] { AccountingServiceType.CatchUpBookkeeping, AccountingServiceType.CorporateTaxPreparation },
            extractedApproximateAnnualRevenue: 1_000_000m,
            estimatedDeadlineInDays: 1,
            bookkeepingMonthsBehind: 6);

        var result = Service.Qualify(lead, analysis);

        // The five dimension maximums (30+25+25+15+5) sum to exactly 100, so this is the
        // highest score reachable through legitimate scoring today — the Math.Clamp to 100 in
        // LeadQualificationService is defense-in-depth against a future rule change, not a
        // currently-exercisable path.
        Assert.Equal(100, result.Score);
        Assert.Equal(LeadPriority.High, result.Priority);
    }

    [Fact]
    public void Qualify_CalledTwiceWithEquivalentInputs_ProducesIdenticalScorePriorityAndReasons()
    {
        var lead = CreateLead(clientType: ClientType.Corporation, approximateAnnualRevenue: 700_000m);
        var analysisOne = CreateAnalysis(
            requestedServices: new[] { AccountingServiceType.CatchUpBookkeeping },
            estimatedDeadlineInDays: 21,
            bookkeepingMonthsBehind: 6);
        var analysisTwo = CreateAnalysis(
            requestedServices: new[] { AccountingServiceType.CatchUpBookkeeping },
            estimatedDeadlineInDays: 21,
            bookkeepingMonthsBehind: 6);

        var resultOne = Service.Qualify(lead, analysisOne);
        var resultTwo = Service.Qualify(lead, analysisTwo);

        Assert.Equal(resultOne.Score, resultTwo.Score);
        Assert.Equal(resultOne.Priority, resultTwo.Priority);
        Assert.Equal(resultOne.Reasons, resultTwo.Reasons);
    }

    // ---- Full worked example — qualification-rules-v1.md §11 --------------------------------

    [Fact]
    public void Qualify_WorkedExampleFromRulesDoc_ProducesScoreNinetyAndHighPriority()
    {
        // "I own an incorporated consulting company doing around $700k in annual revenue. Our
        // bookkeeping is about six months behind and our corporate return needs to be filed in
        // three weeks. We use QuickBooks Online." — the same inquiry used in
        // docs/requirements.md's Example Scenario and docs/qualification-rules-v1.md §11.
        var lead = CreateLead(
            clientType: ClientType.Corporation,
            approximateAnnualRevenue: 700_000m,
            accountingSoftware: "QuickBooks Online");
        var analysis = CreateAnalysis(
            leadId: lead.Id,
            requestedServices: new[] { AccountingServiceType.CatchUpBookkeeping, AccountingServiceType.CorporateTaxPreparation },
            estimatedDeadlineInDays: 21,
            bookkeepingMonthsBehind: 6);

        var result = Service.Qualify(lead, analysis);

        Assert.Equal(90, result.Score);
        Assert.Equal(LeadPriority.High, result.Priority);
    }
}
