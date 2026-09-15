using AccountingLeads.Domain.Exceptions;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.UnitTests.Domain.Leads;

/// <summary>
/// Unit tests for <see cref="LeadAnalysis"/>'s only construction path, <see cref="LeadAnalysis.Create"/>.
/// Mirrors <see cref="LeadTests"/>'s style: no EF Core/ASP.NET Core dependency.
/// </summary>
public class LeadAnalysisTests
{
    private static readonly Guid ValidLeadId = Guid.NewGuid();
    private static readonly IReadOnlyCollection<AccountingServiceType> ValidRequestedServices =
        new[] { AccountingServiceType.Bookkeeping, AccountingServiceType.PersonalTaxPreparation };

    private static LeadAnalysis CreateValidAnalysis(
        Guid? leadId = null,
        IReadOnlyCollection<AccountingServiceType>? requestedServices = null,
        ClientType? extractedClientType = null,
        decimal? extractedApproximateAnnualRevenue = null,
        string? extractedAccountingSoftware = null,
        Urgency? urgency = null,
        string? extractedFilingDeadline = null,
        string? notes = null,
        int? estimatedDeadlineInDays = null,
        int? bookkeepingMonthsBehind = null) =>
        LeadAnalysis.Create(
            leadId ?? ValidLeadId,
            requestedServices ?? ValidRequestedServices,
            extractedClientType,
            extractedApproximateAnnualRevenue,
            extractedAccountingSoftware,
            urgency,
            extractedFilingDeadline,
            notes,
            estimatedDeadlineInDays,
            bookkeepingMonthsBehind);

    // ---- Happy path ---------------------------------------------------------

    [Fact]
    public void Create_WhenOnlyRequiredFieldsProvided_Succeeds()
    {
        var analysis = CreateValidAnalysis();

        Assert.Equal(ValidLeadId, analysis.LeadId);
        Assert.Equal(ValidRequestedServices, analysis.RequestedServices);
        Assert.Null(analysis.ExtractedClientType);
        Assert.Null(analysis.ExtractedApproximateAnnualRevenue);
        Assert.Null(analysis.ExtractedAccountingSoftware);
        Assert.Null(analysis.Urgency);
        Assert.Null(analysis.ExtractedFilingDeadline);
        Assert.Null(analysis.Notes);
        Assert.Null(analysis.EstimatedDeadlineInDays);
        Assert.Null(analysis.BookkeepingMonthsBehind);
    }

    [Fact]
    public void Create_WhenAllFieldsIncludingOptionalOnesProvided_Succeeds()
    {
        var analysis = CreateValidAnalysis(
            extractedClientType: ClientType.SoleProprietorship,
            extractedApproximateAnnualRevenue: 250_000m,
            extractedAccountingSoftware: "QuickBooks",
            urgency: Urgency.High,
            extractedFilingDeadline: "in about three weeks",
            notes: "Prospect mentioned a recent CRA notice.",
            estimatedDeadlineInDays: 21,
            bookkeepingMonthsBehind: 6);

        Assert.Equal(ClientType.SoleProprietorship, analysis.ExtractedClientType);
        Assert.Equal(250_000m, analysis.ExtractedApproximateAnnualRevenue);
        Assert.Equal("QuickBooks", analysis.ExtractedAccountingSoftware);
        Assert.Equal(Urgency.High, analysis.Urgency);
        Assert.Equal("in about three weeks", analysis.ExtractedFilingDeadline);
        Assert.Equal("Prospect mentioned a recent CRA notice.", analysis.Notes);
        Assert.Equal(21, analysis.EstimatedDeadlineInDays);
        Assert.Equal(6, analysis.BookkeepingMonthsBehind);
    }

    [Fact]
    public void Create_GeneratesNonEmptyId()
    {
        var analysis = CreateValidAnalysis();

        Assert.NotEqual(Guid.Empty, analysis.Id);
    }

    [Fact]
    public void Create_SetsCreatedAtUtcCloseToNow()
    {
        var before = DateTime.UtcNow;

        var analysis = CreateValidAnalysis();

        var after = DateTime.UtcNow;
        Assert.InRange(analysis.CreatedAtUtc, before.AddSeconds(-5), after.AddSeconds(5));
    }

    // ---- LeadId (required) ---------------------------------------------------

    [Fact]
    public void Create_WhenLeadIdIsEmpty_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidAnalysis(leadId: Guid.Empty));
    }

    // ---- RequestedServices (empty allowed, undefined values rejected) --------

    [Fact]
    public void Create_WhenRequestedServicesIsEmpty_Succeeds()
    {
        var analysis = CreateValidAnalysis(requestedServices: Array.Empty<AccountingServiceType>());

        Assert.Empty(analysis.RequestedServices);
    }

    [Fact]
    public void Create_WhenRequestedServicesIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LeadAnalysis.Create(
            ValidLeadId, null!, null, null, null, null, null, null, null, null));
    }

    [Fact]
    public void Create_WhenRequestedServicesContainsUndefinedEnumValue_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateValidAnalysis(requestedServices: new[] { (AccountingServiceType)99 }));
    }

    [Theory]
    [InlineData(AccountingServiceType.Bookkeeping)]
    [InlineData(AccountingServiceType.CatchUpBookkeeping)]
    [InlineData(AccountingServiceType.CorporateTaxPreparation)]
    [InlineData(AccountingServiceType.PersonalTaxPreparation)]
    [InlineData(AccountingServiceType.Payroll)]
    [InlineData(AccountingServiceType.Advisory)]
    [InlineData(AccountingServiceType.AccountingSystemSetup)]
    [InlineData(AccountingServiceType.Other)]
    public void Create_WhenRequestedServicesContainsEachDefinedValue_Succeeds(AccountingServiceType serviceType)
    {
        var analysis = CreateValidAnalysis(requestedServices: new[] { serviceType });

        Assert.Contains(serviceType, analysis.RequestedServices);
    }

    // ---- ExtractedClientType (optional) ---------------------------------------

    [Fact]
    public void Create_WhenExtractedClientTypeIsUndefinedEnumValue_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateValidAnalysis(extractedClientType: (ClientType)99));
    }

    // ---- ExtractedApproximateAnnualRevenue (optional, non-negative) -----------

    [Fact]
    public void Create_WhenExtractedApproximateAnnualRevenueIsNegative_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateValidAnalysis(extractedApproximateAnnualRevenue: -0.01m));
    }

    [Fact]
    public void Create_WhenExtractedApproximateAnnualRevenueIsExactlyAtMax_Succeeds()
    {
        var analysis = CreateValidAnalysis(extractedApproximateAnnualRevenue: Lead.MaxApproximateAnnualRevenue);

        Assert.Equal(Lead.MaxApproximateAnnualRevenue, analysis.ExtractedApproximateAnnualRevenue);
    }

    [Fact]
    public void Create_WhenExtractedApproximateAnnualRevenueExceedsMax_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateValidAnalysis(extractedApproximateAnnualRevenue: Lead.MaxApproximateAnnualRevenue + 0.01m));
    }

    // ---- Urgency (optional) ----------------------------------------------------

    [Fact]
    public void Create_WhenUrgencyIsUndefinedEnumValue_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidAnalysis(urgency: (Urgency)99));
    }

    // ---- String field length boundaries ---------------------------------------

    [Fact]
    public void Create_WhenExtractedAccountingSoftwareIsExactlyAtMaxLength_Succeeds()
    {
        var value = new string('S', LeadAnalysis.ExtractedAccountingSoftwareMaxLength);

        var analysis = CreateValidAnalysis(extractedAccountingSoftware: value);

        Assert.Equal(value, analysis.ExtractedAccountingSoftware);
    }

    [Fact]
    public void Create_WhenExtractedAccountingSoftwareExceedsMaxLength_ThrowsDomainValidationException()
    {
        var value = new string('S', LeadAnalysis.ExtractedAccountingSoftwareMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => CreateValidAnalysis(extractedAccountingSoftware: value));
    }

    [Fact]
    public void Create_WhenExtractedFilingDeadlineIsExactlyAtMaxLength_Succeeds()
    {
        var value = new string('D', LeadAnalysis.ExtractedFilingDeadlineMaxLength);

        var analysis = CreateValidAnalysis(extractedFilingDeadline: value);

        Assert.Equal(value, analysis.ExtractedFilingDeadline);
    }

    [Fact]
    public void Create_WhenExtractedFilingDeadlineExceedsMaxLength_ThrowsDomainValidationException()
    {
        var value = new string('D', LeadAnalysis.ExtractedFilingDeadlineMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => CreateValidAnalysis(extractedFilingDeadline: value));
    }

    [Fact]
    public void Create_WhenNotesIsExactlyAtMaxLength_Succeeds()
    {
        var value = new string('N', LeadAnalysis.NotesMaxLength);

        var analysis = CreateValidAnalysis(notes: value);

        Assert.Equal(value, analysis.Notes);
    }

    [Fact]
    public void Create_WhenNotesExceedsMaxLength_ThrowsDomainValidationException()
    {
        var value = new string('N', LeadAnalysis.NotesMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => CreateValidAnalysis(notes: value));
    }

    // ---- EstimatedDeadlineInDays (optional, negative allowed = overdue) / BookkeepingMonthsBehind (optional, non-negative) ----------

    [Fact]
    public void Create_WhenEstimatedDeadlineInDaysIsZero_Succeeds()
    {
        var analysis = CreateValidAnalysis(estimatedDeadlineInDays: 0);

        Assert.Equal(0, analysis.EstimatedDeadlineInDays);
    }

    [Fact]
    public void Create_WhenEstimatedDeadlineInDaysIsNegative_Succeeds()
    {
        // An overdue deadline (e.g. "the return was due three days ago") is a valid, expected
        // input — qualification-rules-v1.md §4 scores it in the highest urgency band, not as an
        // error, so it must not be rejected here.
        var analysis = CreateValidAnalysis(estimatedDeadlineInDays: -3);

        Assert.Equal(-3, analysis.EstimatedDeadlineInDays);
    }

    [Fact]
    public void Create_WhenBookkeepingMonthsBehindIsNegative_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidAnalysis(bookkeepingMonthsBehind: -1));
    }

    [Fact]
    public void Create_WhenBookkeepingMonthsBehindIsZero_Succeeds()
    {
        var analysis = CreateValidAnalysis(bookkeepingMonthsBehind: 0);

        Assert.Equal(0, analysis.BookkeepingMonthsBehind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenOptionalStringFieldsAreEmptyOrWhitespace_NormalizeToNull(string value)
    {
        var analysis = CreateValidAnalysis(
            extractedAccountingSoftware: value,
            extractedFilingDeadline: value,
            notes: value);

        Assert.Null(analysis.ExtractedAccountingSoftware);
        Assert.Null(analysis.ExtractedFilingDeadline);
        Assert.Null(analysis.Notes);
    }
}
