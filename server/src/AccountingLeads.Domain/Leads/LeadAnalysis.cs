using AccountingLeads.Domain.Exceptions;

namespace AccountingLeads.Domain.Leads;

public sealed class LeadAnalysis
{
    public const int ExtractedAccountingSoftwareMaxLength = 100;
    public const int ExtractedFilingDeadlineMaxLength = 200;
    public const int NotesMaxLength = 2000;

    public Guid Id { get; private set; }
    public Guid LeadId { get; private set; }
    public IReadOnlyCollection<AccountingServiceType> RequestedServices { get; private set; } = Array.Empty<AccountingServiceType>();
    public ClientType? ExtractedClientType { get; private set; }
    public decimal? ExtractedApproximateAnnualRevenue { get; private set; }
    public string? ExtractedAccountingSoftware { get; private set; }
    public Urgency? Urgency { get; private set; }
    public string? ExtractedFilingDeadline { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// AI-estimated whole number of days from the lead's submission date until any filing/work
    /// deadline mentioned in the inquiry (e.g. "in about three weeks" → ~21). Null when no
    /// deadline was mentioned or the inquiry doesn't give enough information to estimate one.
    /// May be negative — an overdue deadline (e.g. "the return was due three days ago" → -3) is
    /// deliberately allowed rather than rejected, since qualification-rules-v1.md §4 scores
    /// overdue/due-today deadlines in its highest urgency band, not as an error. Distinct from
    /// <see cref="ExtractedFilingDeadline"/>'s free text: this field exists so deterministic
    /// qualification scoring has a number to bucket, rather than needing to parse relative
    /// language itself.
    /// </summary>
    public int? EstimatedDeadlineInDays { get; private set; }

    /// <summary>
    /// AI-estimated whole number of months of bookkeeping backlog mentioned in the inquiry (e.g.
    /// "about six months behind" → 6). Null when backlog wasn't mentioned. Feeds deterministic
    /// qualification scoring (docs/qualification-rules-v1.md §5).
    /// </summary>
    public int? BookkeepingMonthsBehind { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private LeadAnalysis()
    {
        // Reserved for future EF Core materialization (Infrastructure slice).
    }

    public static LeadAnalysis Create(
        Guid leadId,
        IReadOnlyCollection<AccountingServiceType> requestedServices,
        ClientType? extractedClientType,
        decimal? extractedApproximateAnnualRevenue,
        string? extractedAccountingSoftware,
        Urgency? urgency,
        string? extractedFilingDeadline,
        string? notes,
        int? estimatedDeadlineInDays,
        int? bookkeepingMonthsBehind)
    {
        if (leadId == Guid.Empty)
        {
            throw new DomainValidationException("leadId is required.");
        }

        ArgumentNullException.ThrowIfNull(requestedServices);

        foreach (var serviceType in requestedServices)
        {
            if (!Enum.IsDefined(serviceType))
            {
                throw new DomainValidationException($"requestedServices contains an unsupported value: {serviceType}.");
            }
        }

        if (extractedClientType is not null && !Enum.IsDefined(extractedClientType.Value))
        {
            throw new DomainValidationException("extractedClientType is not a supported value.");
        }

        if (extractedApproximateAnnualRevenue is < 0)
        {
            throw new DomainValidationException("extractedApproximateAnnualRevenue cannot be negative.");
        }

        if (extractedApproximateAnnualRevenue > Lead.MaxApproximateAnnualRevenue)
        {
            throw new DomainValidationException(
                $"extractedApproximateAnnualRevenue cannot exceed {Lead.MaxApproximateAnnualRevenue}.");
        }

        if (urgency is not null && !Enum.IsDefined(urgency.Value))
        {
            throw new DomainValidationException("urgency is not a supported value.");
        }

        if (bookkeepingMonthsBehind is < 0)
        {
            throw new DomainValidationException("bookkeepingMonthsBehind cannot be negative.");
        }

        var normalizedAccountingSoftware = NormalizeOptional(
            extractedAccountingSoftware, nameof(extractedAccountingSoftware), ExtractedAccountingSoftwareMaxLength);
        var normalizedFilingDeadline = NormalizeOptional(
            extractedFilingDeadline, nameof(extractedFilingDeadline), ExtractedFilingDeadlineMaxLength);
        var normalizedNotes = NormalizeOptional(notes, nameof(notes), NotesMaxLength);

        return new LeadAnalysis
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            RequestedServices = requestedServices.ToArray(),
            ExtractedClientType = extractedClientType,
            ExtractedApproximateAnnualRevenue = extractedApproximateAnnualRevenue,
            ExtractedAccountingSoftware = normalizedAccountingSoftware,
            Urgency = urgency,
            ExtractedFilingDeadline = normalizedFilingDeadline,
            Notes = normalizedNotes,
            EstimatedDeadlineInDays = estimatedDeadlineInDays,
            BookkeepingMonthsBehind = bookkeepingMonthsBehind,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static string? NormalizeOptional(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException($"{fieldName} exceeds maximum length of {maxLength}.");
        }

        return trimmed;
    }
}
