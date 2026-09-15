using AccountingLeads.Domain.Exceptions;

namespace AccountingLeads.Domain.Leads;

/// <summary>
/// Deterministic qualification outcome for a <see cref="Lead"/>, computed from its
/// <see cref="LeadAnalysis"/> by the Application-layer scoring service — see
/// docs/qualification-rules-v1.md for the business rules this entity's fields represent.
/// </summary>
public sealed class LeadQualificationResult
{
    public const int MinScore = 0;
    public const int MaxScore = 100;

    /// <summary>
    /// Identifies which revision of docs/qualification-rules-v1.md produced this result, so a
    /// future rules change doesn't make historical rows indistinguishable from freshly-scored
    /// ones.
    /// </summary>
    public const string RulesVersion = "v1";

    public Guid Id { get; private set; }
    public Guid LeadId { get; private set; }
    public int Score { get; private set; }
    public LeadPriority Priority { get; private set; }
    public IReadOnlyCollection<string> Reasons { get; private set; } = Array.Empty<string>();
    public string RulesVersionApplied { get; private set; } = RulesVersion;
    public DateTime CreatedAtUtc { get; private set; }

    private LeadQualificationResult()
    {
        // Reserved for future EF Core materialization (Infrastructure slice).
    }

    public static LeadQualificationResult Create(Guid leadId, int score, IReadOnlyCollection<string> reasons)
    {
        if (leadId == Guid.Empty)
        {
            throw new DomainValidationException("leadId is required.");
        }

        ArgumentNullException.ThrowIfNull(reasons);

        if (score < MinScore || score > MaxScore)
        {
            throw new DomainValidationException($"score must be between {MinScore} and {MaxScore}.");
        }

        return new LeadQualificationResult
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            Score = score,
            Priority = PriorityFromScore(score),
            Reasons = reasons.ToArray(),
            RulesVersionApplied = RulesVersion,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Score-to-priority thresholds from docs/qualification-rules-v1.md §1/§9. Owned here,
    /// inside the factory, so a persisted result's Priority can never drift from its Score.
    /// </summary>
    private static LeadPriority PriorityFromScore(int score) => score switch
    {
        >= 70 => LeadPriority.High,
        >= 40 => LeadPriority.Medium,
        _ => LeadPriority.Low
    };
}
