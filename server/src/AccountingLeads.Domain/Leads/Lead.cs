using System.Globalization;
using System.Text.RegularExpressions;
using AccountingLeads.Domain.Exceptions;

namespace AccountingLeads.Domain.Leads;

public sealed class Lead
{
    public const int NameMaxLength = 150;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 50;
    public const int CompanyNameMaxLength = 200;
    public const int AccountingSoftwareMaxLength = 100;
    public const int InquiryMaxLength = 4000;

    /// <summary>
    /// Matches the capacity of the <c>decimal(18,2)</c> storage column for
    /// <see cref="ApproximateAnnualRevenue"/> (see <c>LeadConfiguration</c> in Infrastructure).
    /// Kept as a string because attribute arguments (e.g. <c>RangeAttribute</c> on the Api
    /// request DTO) must be compile-time constants and cannot reference a <c>decimal</c> value
    /// directly.
    /// </summary>
    public const string MaxApproximateAnnualRevenueLiteral = "9999999999999999.99";

    public static readonly decimal MaxApproximateAnnualRevenue =
        decimal.Parse(MaxApproximateAnnualRevenueLiteral, CultureInfo.InvariantCulture);

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? CompanyName { get; private set; }
    public ClientType? ClientType { get; private set; }
    public decimal? ApproximateAnnualRevenue { get; private set; }
    public string? AccountingSoftware { get; private set; }
    public string Inquiry { get; private set; } = string.Empty;
    public LeadStatus LeadStatus { get; private set; }
    public AiProcessingStatus AiProcessingStatus { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Lead()
    {
        // Reserved for future EF Core materialization (Infrastructure slice).
    }

    public static Lead Create(
        string name,
        string email,
        string? phone,
        string? companyName,
        ClientType? clientType,
        decimal? approximateAnnualRevenue,
        string? accountingSoftware,
        string inquiry)
    {
        var normalizedName = RequireNormalized(name, nameof(name), NameMaxLength);
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizeOptional(phone, nameof(phone), PhoneMaxLength);
        var normalizedCompanyName = NormalizeOptional(companyName, nameof(companyName), CompanyNameMaxLength);
        var normalizedAccountingSoftware = NormalizeOptional(accountingSoftware, nameof(accountingSoftware), AccountingSoftwareMaxLength);
        var normalizedInquiry = RequireNormalized(inquiry, nameof(inquiry), InquiryMaxLength);

        if (approximateAnnualRevenue is < 0)
        {
            throw new DomainValidationException("Approximate annual revenue cannot be negative.");
        }

        if (approximateAnnualRevenue > MaxApproximateAnnualRevenue)
        {
            throw new DomainValidationException(
                $"Approximate annual revenue cannot exceed {MaxApproximateAnnualRevenue}.");
        }

        if (clientType is not null && !Enum.IsDefined(clientType.Value))
        {
            throw new DomainValidationException("clientType is not a supported value.");
        }

        return new Lead
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Email = normalizedEmail,
            Phone = normalizedPhone,
            CompanyName = normalizedCompanyName,
            ClientType = clientType,
            ApproximateAnnualRevenue = approximateAnnualRevenue,
            AccountingSoftware = normalizedAccountingSoftware,
            Inquiry = normalizedInquiry,
            LeadStatus = LeadStatus.New,
            AiProcessingStatus = AiProcessingStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks AI processing as in progress. Re-enterable from any non-terminal state
    /// (<see cref="AiProcessingStatus.Pending"/> or <see cref="AiProcessingStatus.Failed"/>) —
    /// unlike <c>OutboxMessage</c>'s one-way publish state, a Service Bus redelivery after a
    /// crash or transient failure must be able to call this again, so only the one true
    /// terminal state (<see cref="AiProcessingStatus.Completed"/>) is guarded against.
    /// </summary>
    public void BeginAiProcessing()
    {
        if (AiProcessingStatus == AiProcessingStatus.Completed)
        {
            throw new InvalidOperationException($"Lead {Id} has already completed AI processing.");
        }

        AiProcessingStatus = AiProcessingStatus.Processing;
    }

    public void CompleteAiProcessing()
    {
        if (AiProcessingStatus == AiProcessingStatus.Completed)
        {
            throw new InvalidOperationException($"Lead {Id} has already completed AI processing.");
        }

        AiProcessingStatus = AiProcessingStatus.Completed;
    }

    public void FailAiProcessing()
    {
        if (AiProcessingStatus == AiProcessingStatus.Completed)
        {
            throw new InvalidOperationException($"Lead {Id} has already completed AI processing.");
        }

        AiProcessingStatus = AiProcessingStatus.Failed;
    }

    /// <summary>
    /// Marks the lead as qualified once a <see cref="LeadQualificationResult"/> has been
    /// computed for it. A one-way transition — there is currently no rule under which a
    /// qualified lead reverts to <see cref="LeadStatus.New"/>.
    /// </summary>
    public void MarkQualified()
    {
        if (LeadStatus == LeadStatus.Qualified)
        {
            throw new InvalidOperationException($"Lead {Id} has already been qualified.");
        }

        LeadStatus = LeadStatus.Qualified;
    }

    private static string RequireNormalized(string? value, string fieldName, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainValidationException($"{fieldName} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException($"{fieldName} exceeds maximum length of {maxLength}.");
        }

        return trimmed;
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

    private static string NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainValidationException("email is required.");
        }

        if (trimmed.Length > EmailMaxLength)
        {
            throw new DomainValidationException($"email exceeds maximum length of {EmailMaxLength}.");
        }

        if (!EmailPattern.IsMatch(trimmed))
        {
            throw new DomainValidationException("email is not a valid email address.");
        }

        return trimmed;
    }
}
