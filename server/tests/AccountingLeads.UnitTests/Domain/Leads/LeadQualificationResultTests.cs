using AccountingLeads.Domain.Exceptions;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.UnitTests.Domain.Leads;

/// <summary>
/// Unit tests for <see cref="LeadQualificationResult"/>'s only construction path,
/// <see cref="LeadQualificationResult.Create"/>. Mirrors <see cref="LeadAnalysisTests"/>'s style:
/// no EF Core/ASP.NET Core dependency.
/// </summary>
public class LeadQualificationResultTests
{
    private static readonly Guid ValidLeadId = Guid.NewGuid();
    private static readonly IReadOnlyCollection<string> ValidReasons = new[] { "Annual revenue is $1,000,000 or more." };

    private static LeadQualificationResult CreateValidResult(
        Guid? leadId = null,
        int score = 50,
        IReadOnlyCollection<string>? reasons = null) =>
        LeadQualificationResult.Create(leadId ?? ValidLeadId, score, reasons ?? ValidReasons);

    // ---- Happy path ---------------------------------------------------------

    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var result = CreateValidResult();

        Assert.Equal(ValidLeadId, result.LeadId);
        Assert.Equal(50, result.Score);
        Assert.Equal(ValidReasons, result.Reasons);
        Assert.Equal(LeadQualificationResult.RulesVersion, result.RulesVersionApplied);
    }

    [Fact]
    public void Create_GeneratesNonEmptyId()
    {
        var result = CreateValidResult();

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public void Create_SetsCreatedAtUtcCloseToNow()
    {
        var before = DateTime.UtcNow;

        var result = CreateValidResult();

        var after = DateTime.UtcNow;
        Assert.InRange(result.CreatedAtUtc, before.AddSeconds(-5), after.AddSeconds(5));
    }

    [Fact]
    public void Create_WhenReasonsIsEmpty_Succeeds()
    {
        var result = CreateValidResult(reasons: Array.Empty<string>());

        Assert.Empty(result.Reasons);
    }

    // ---- LeadId (required) ---------------------------------------------------

    [Fact]
    public void Create_WhenLeadIdIsEmpty_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidResult(leadId: Guid.Empty));
    }

    // ---- Reasons (required, non-null) -----------------------------------------

    [Fact]
    public void Create_WhenReasonsIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LeadQualificationResult.Create(ValidLeadId, 50, null!));
    }

    // ---- Score / Priority boundaries -------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_WhenScoreIsOutOfRange_ThrowsDomainValidationException(int score)
    {
        Assert.Throws<DomainValidationException>(() => CreateValidResult(score: score));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_WhenScoreIsAtBoundary_Succeeds(int score)
    {
        var result = CreateValidResult(score: score);

        Assert.Equal(score, result.Score);
    }

    [Theory]
    [InlineData(0, LeadPriority.Low)]
    [InlineData(39, LeadPriority.Low)]
    [InlineData(40, LeadPriority.Medium)]
    [InlineData(69, LeadPriority.Medium)]
    [InlineData(70, LeadPriority.High)]
    [InlineData(100, LeadPriority.High)]
    public void Create_DerivesPriorityFromScore(int score, LeadPriority expectedPriority)
    {
        var result = CreateValidResult(score: score);

        Assert.Equal(expectedPriority, result.Priority);
    }
}
