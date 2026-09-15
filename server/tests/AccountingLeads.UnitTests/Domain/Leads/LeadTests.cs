using AccountingLeads.Domain.Exceptions;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.UnitTests.Domain.Leads;

/// <summary>
/// Unit tests for <see cref="Lead"/>'s only construction path, <see cref="Lead.Create"/>.
/// These exercise Domain invariants directly (docs/architecture.md §17 "Lead Capture —
/// Initial Domain Decisions") with no EF Core/ASP.NET Core dependency, matching Domain's own
/// zero-dependency constraint.
/// </summary>
public class LeadTests
{
    private const string ValidName = "Jane Accountant";
    private const string ValidEmail = "jane@example.com";
    private const string ValidInquiry = "I need help with catch-up bookkeeping for my small business.";

    private static Lead CreateValidLead(
        string name = ValidName,
        string email = ValidEmail,
        string? phone = null,
        string? companyName = null,
        ClientType? clientType = null,
        decimal? approximateAnnualRevenue = null,
        string? accountingSoftware = null,
        string inquiry = ValidInquiry) =>
        Lead.Create(name, email, phone, companyName, clientType, approximateAnnualRevenue, accountingSoftware, inquiry);

    // ---- Happy path ---------------------------------------------------------

    [Fact]
    public void Create_WhenOnlyRequiredFieldsProvided_Succeeds()
    {
        var lead = CreateValidLead();

        Assert.Equal(ValidName, lead.Name);
        Assert.Equal(ValidEmail, lead.Email);
        Assert.Equal(ValidInquiry, lead.Inquiry);
        Assert.Null(lead.Phone);
        Assert.Null(lead.CompanyName);
        Assert.Null(lead.ClientType);
        Assert.Null(lead.ApproximateAnnualRevenue);
        Assert.Null(lead.AccountingSoftware);
    }

    [Fact]
    public void Create_WhenAllFieldsIncludingOptionalOnesProvided_Succeeds()
    {
        var lead = CreateValidLead(
            phone: "+1 (555) 123-4567",
            companyName: "Acme Bookkeeping LLC",
            clientType: ClientType.SoleProprietorship,
            approximateAnnualRevenue: 250_000m,
            accountingSoftware: "QuickBooks");

        Assert.Equal("+1 (555) 123-4567", lead.Phone);
        Assert.Equal("Acme Bookkeeping LLC", lead.CompanyName);
        Assert.Equal(ClientType.SoleProprietorship, lead.ClientType);
        Assert.Equal(250_000m, lead.ApproximateAnnualRevenue);
        Assert.Equal("QuickBooks", lead.AccountingSoftware);
    }

    // ---- System-generated fields ---------------------------------------------

    [Fact]
    public void Create_GeneratesNonEmptyId()
    {
        var lead = CreateValidLead();

        Assert.NotEqual(Guid.Empty, lead.Id);
    }

    [Fact]
    public void Create_CalledTwice_ProducesDifferentIds()
    {
        var first = CreateValidLead();
        var second = CreateValidLead();

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_SetsCreatedAtUtcCloseToNow()
    {
        var before = DateTime.UtcNow;

        var lead = CreateValidLead();

        var after = DateTime.UtcNow;
        Assert.InRange(lead.CreatedAtUtc, before.AddSeconds(-5), after.AddSeconds(5));
    }

    [Fact]
    public void Create_SetsLeadStatusToNew()
    {
        var lead = CreateValidLead();

        Assert.Equal(LeadStatus.New, lead.LeadStatus);
    }

    [Fact]
    public void Create_SetsAiProcessingStatusToPending()
    {
        var lead = CreateValidLead();

        Assert.Equal(AiProcessingStatus.Pending, lead.AiProcessingStatus);
    }

    // ---- Name (required) -----------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameEmptyOrWhitespace_ThrowsDomainValidationException(string name)
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(name: name));
    }

    [Fact]
    public void Create_WhenNameIsNull_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(name: null!));
    }

    [Fact]
    public void Create_WhenNameIsExactlyAtMaxLength_Succeeds()
    {
        var name = new string('N', Lead.NameMaxLength);

        var lead = CreateValidLead(name: name);

        Assert.Equal(name, lead.Name);
    }

    [Fact]
    public void Create_WhenNameExceedsMaxLength_ThrowsDomainValidationException()
    {
        var name = new string('N', Lead.NameMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => CreateValidLead(name: name));
    }

    [Fact]
    public void Create_TrimsName()
    {
        var lead = CreateValidLead(name: "  Jane Accountant  ");

        Assert.Equal("Jane Accountant", lead.Name);
    }

    // ---- Email (required) -----------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenEmailEmptyOrWhitespace_ThrowsDomainValidationException(string email)
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(email: email));
    }

    [Fact]
    public void Create_WhenEmailIsNull_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(email: null!));
    }

    [Fact]
    public void Create_WhenEmailIsExactlyAtMaxLength_Succeeds()
    {
        var local = "user@";
        var domain = new string('a', Lead.EmailMaxLength - local.Length - 4) + ".com";
        var email = local + domain;
        Assert.Equal(Lead.EmailMaxLength, email.Length);

        var lead = CreateValidLead(email: email);

        Assert.Equal(email, lead.Email);
    }

    [Fact]
    public void Create_WhenEmailExceedsMaxLength_ThrowsDomainValidationException()
    {
        var local = "user@";
        var domain = new string('a', Lead.EmailMaxLength + 1 - local.Length - 4) + ".com";
        var email = local + domain;
        Assert.Equal(Lead.EmailMaxLength + 1, email.Length);

        Assert.Throws<DomainValidationException>(() => CreateValidLead(email: email));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.example.com")]
    [InlineData("user@nodotafterat")]
    public void Create_WhenEmailIsMalformed_ThrowsDomainValidationException(string email)
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(email: email));
    }

    [Theory]
    [InlineData("jane@example.com")]
    [InlineData("first.last+tag@sub.example.co")]
    public void Create_WhenEmailIsWellFormed_Succeeds(string email)
    {
        var lead = CreateValidLead(email: email);

        Assert.Equal(email.ToLowerInvariant(), lead.Email);
    }

    [Fact]
    public void Create_TrimsAndLowercasesEmail()
    {
        var lead = CreateValidLead(email: "  Jane@Example.COM  ");

        Assert.Equal("jane@example.com", lead.Email);
    }

    // ---- Inquiry (required) ----------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenInquiryEmptyOrWhitespace_ThrowsDomainValidationException(string inquiry)
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(inquiry: inquiry));
    }

    [Fact]
    public void Create_WhenInquiryIsNull_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(inquiry: null!));
    }

    [Fact]
    public void Create_WhenInquiryIsExactlyAtMaxLength_Succeeds()
    {
        var inquiry = new string('I', Lead.InquiryMaxLength);

        var lead = CreateValidLead(inquiry: inquiry);

        Assert.Equal(inquiry, lead.Inquiry);
    }

    [Fact]
    public void Create_WhenInquiryExceedsMaxLength_ThrowsDomainValidationException()
    {
        var inquiry = new string('I', Lead.InquiryMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => CreateValidLead(inquiry: inquiry));
    }

    [Fact]
    public void Create_TrimsInquiry()
    {
        var lead = CreateValidLead(inquiry: "  " + ValidInquiry + "  ");

        Assert.Equal(ValidInquiry, lead.Inquiry);
    }

    // ---- Phone (optional, permissive format) -----------------------------------

    [Fact]
    public void Create_WhenPhoneOmitted_LeavesPhoneNull()
    {
        var lead = CreateValidLead(phone: null);

        Assert.Null(lead.Phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenPhoneIsEmptyOrWhitespace_NormalizesToNull(string phone)
    {
        var lead = CreateValidLead(phone: phone);

        Assert.Null(lead.Phone);
    }

    [Fact]
    public void Create_WhenPhoneIsExactlyAtMaxLength_Succeeds()
    {
        var phone = new string('5', Lead.PhoneMaxLength);

        var lead = CreateValidLead(phone: phone);

        Assert.Equal(phone, lead.Phone);
    }

    [Fact]
    public void Create_WhenPhoneExceedsMaxLength_ThrowsDomainValidationException()
    {
        var phone = new string('5', Lead.PhoneMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => CreateValidLead(phone: phone));
    }

    [Theory]
    [InlineData("+1 (555) 123-4567")]
    [InlineData("555.123.4567")]
    [InlineData("+44 20 7946 0958")]
    [InlineData("ext. 1234")]
    public void Create_WhenPhoneHasOddButShortFormat_DoesNotThrow(string phone)
    {
        var lead = CreateValidLead(phone: phone);

        Assert.Equal(phone, lead.Phone);
    }

    [Fact]
    public void Create_TrimsPhone()
    {
        var lead = CreateValidLead(phone: "  555-123-4567  ");

        Assert.Equal("555-123-4567", lead.Phone);
    }

    // ---- CompanyName (optional) -------------------------------------------------

    [Fact]
    public void Create_WhenCompanyNameOmitted_LeavesCompanyNameNull()
    {
        var lead = CreateValidLead(companyName: null);

        Assert.Null(lead.CompanyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenCompanyNameIsEmptyOrWhitespace_NormalizesToNull(string companyName)
    {
        var lead = CreateValidLead(companyName: companyName);

        Assert.Null(lead.CompanyName);
    }

    [Fact]
    public void Create_WhenCompanyNameIsExactlyAtMaxLength_Succeeds()
    {
        var companyName = new string('C', Lead.CompanyNameMaxLength);

        var lead = CreateValidLead(companyName: companyName);

        Assert.Equal(companyName, lead.CompanyName);
    }

    [Fact]
    public void Create_WhenCompanyNameExceedsMaxLength_ThrowsDomainValidationException()
    {
        var companyName = new string('C', Lead.CompanyNameMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => CreateValidLead(companyName: companyName));
    }

    // ---- AccountingSoftware (optional) ------------------------------------------

    [Fact]
    public void Create_WhenAccountingSoftwareOmitted_LeavesAccountingSoftwareNull()
    {
        var lead = CreateValidLead(accountingSoftware: null);

        Assert.Null(lead.AccountingSoftware);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenAccountingSoftwareIsEmptyOrWhitespace_NormalizesToNull(string accountingSoftware)
    {
        var lead = CreateValidLead(accountingSoftware: accountingSoftware);

        Assert.Null(lead.AccountingSoftware);
    }

    [Fact]
    public void Create_WhenAccountingSoftwareIsExactlyAtMaxLength_Succeeds()
    {
        var accountingSoftware = new string('S', Lead.AccountingSoftwareMaxLength);

        var lead = CreateValidLead(accountingSoftware: accountingSoftware);

        Assert.Equal(accountingSoftware, lead.AccountingSoftware);
    }

    [Fact]
    public void Create_WhenAccountingSoftwareExceedsMaxLength_ThrowsDomainValidationException()
    {
        var accountingSoftware = new string('S', Lead.AccountingSoftwareMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => CreateValidLead(accountingSoftware: accountingSoftware));
    }

    // ---- ClientType (optional, passed through untouched) ------------------------

    [Fact]
    public void Create_WhenClientTypeOmitted_LeavesClientTypeNull()
    {
        var lead = CreateValidLead(clientType: null);

        Assert.Null(lead.ClientType);
    }

    [Theory]
    [InlineData(ClientType.Individual)]
    [InlineData(ClientType.SoleProprietorship)]
    [InlineData(ClientType.Corporation)]
    [InlineData(ClientType.LimitedLiabilityCompany)]
    [InlineData(ClientType.Partnership)]
    [InlineData(ClientType.NonProfit)]
    [InlineData(ClientType.Other)]
    public void Create_WhenClientTypeProvided_PassesThroughUnchanged(ClientType clientType)
    {
        var lead = CreateValidLead(clientType: clientType);

        Assert.Equal(clientType, lead.ClientType);
    }

    [Fact]
    public void Create_WhenClientTypeIsUndefinedEnumValue_ThrowsDomainValidationException()
    {
        // (ClientType)99 does not correspond to any defined enum member. This defends against
        // callers that bypass API model binding (e.g. calling Lead.Create directly).
        Assert.Throws<DomainValidationException>(() => CreateValidLead(clientType: (ClientType)99));
    }

    // ---- ApproximateAnnualRevenue (optional, non-negative) -----------------------

    [Fact]
    public void Create_WhenApproximateAnnualRevenueOmitted_LeavesItNull()
    {
        var lead = CreateValidLead(approximateAnnualRevenue: null);

        Assert.Null(lead.ApproximateAnnualRevenue);
    }

    [Fact]
    public void Create_WhenApproximateAnnualRevenueIsZero_Succeeds()
    {
        var lead = CreateValidLead(approximateAnnualRevenue: 0m);

        Assert.Equal(0m, lead.ApproximateAnnualRevenue);
    }

    [Fact]
    public void Create_WhenApproximateAnnualRevenueIsPositive_Succeeds()
    {
        var lead = CreateValidLead(approximateAnnualRevenue: 1_000_000.50m);

        Assert.Equal(1_000_000.50m, lead.ApproximateAnnualRevenue);
    }

    [Fact]
    public void Create_WhenApproximateAnnualRevenueIsSlightlyNegative_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(approximateAnnualRevenue: -0.01m));
    }

    [Fact]
    public void Create_WhenApproximateAnnualRevenueIsLargeNegative_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => CreateValidLead(approximateAnnualRevenue: -1_000_000m));
    }

    [Fact]
    public void Create_WhenApproximateAnnualRevenueIsExactlyAtMax_Succeeds()
    {
        var lead = CreateValidLead(approximateAnnualRevenue: Lead.MaxApproximateAnnualRevenue);

        Assert.Equal(Lead.MaxApproximateAnnualRevenue, lead.ApproximateAnnualRevenue);
    }

    [Fact]
    public void Create_WhenApproximateAnnualRevenueExceedsMax_ThrowsDomainValidationException()
    {
        // Defense-in-depth: even though the Api's [Range] attribute already blocks values
        // above Lead.MaxApproximateAnnualRevenue, Domain must independently reject them too,
        // since Lead.Create can be reached directly by callers that bypass the Api (e.g. the
        // Worker, tests, or a future integration). Without this guard, a value like
        // Lead.MaxApproximateAnnualRevenue + 0.01m would pass Domain validation but fail later
        // when EF Core tries to persist it into the decimal(18,2) storage column.
        Assert.Throws<DomainValidationException>(
            () => CreateValidLead(approximateAnnualRevenue: Lead.MaxApproximateAnnualRevenue + 0.01m));
    }

    // ---- AI processing state transitions ------------------------------------------

    [Fact]
    public void BeginAiProcessing_FromPending_SetsStatusToProcessing()
    {
        var lead = CreateValidLead();

        lead.BeginAiProcessing();

        Assert.Equal(AiProcessingStatus.Processing, lead.AiProcessingStatus);
    }

    [Fact]
    public void CompleteAiProcessing_FromProcessing_SetsStatusToCompleted()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();

        lead.CompleteAiProcessing();

        Assert.Equal(AiProcessingStatus.Completed, lead.AiProcessingStatus);
    }

    [Fact]
    public void FailAiProcessing_FromProcessing_SetsStatusToFailed()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();

        lead.FailAiProcessing();

        Assert.Equal(AiProcessingStatus.Failed, lead.AiProcessingStatus);
    }

    [Fact]
    public void BeginAiProcessing_AfterFailed_SucceedsAndReturnsToProcessing()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();
        lead.FailAiProcessing();

        lead.BeginAiProcessing();

        Assert.Equal(AiProcessingStatus.Processing, lead.AiProcessingStatus);
    }

    [Fact]
    public void BeginAiProcessing_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();
        lead.CompleteAiProcessing();

        Assert.Throws<InvalidOperationException>(() => lead.BeginAiProcessing());
    }

    [Fact]
    public void CompleteAiProcessing_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();
        lead.CompleteAiProcessing();

        Assert.Throws<InvalidOperationException>(() => lead.CompleteAiProcessing());
    }

    [Fact]
    public void FailAiProcessing_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();
        lead.CompleteAiProcessing();

        Assert.Throws<InvalidOperationException>(() => lead.FailAiProcessing());
    }

    [Fact]
    public void MarkQualified_FromNew_SetsLeadStatusToQualified()
    {
        var lead = CreateValidLead();

        lead.MarkQualified();

        Assert.Equal(LeadStatus.Qualified, lead.LeadStatus);
    }

    [Fact]
    public void MarkQualified_WhenAlreadyQualified_ThrowsInvalidOperationException()
    {
        var lead = CreateValidLead();
        lead.MarkQualified();

        Assert.Throws<InvalidOperationException>(() => lead.MarkQualified());
    }
}
