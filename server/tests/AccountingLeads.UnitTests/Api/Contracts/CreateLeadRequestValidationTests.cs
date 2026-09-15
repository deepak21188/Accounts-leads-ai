using System.ComponentModel.DataAnnotations;
using AccountingLeads.Api.Contracts;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.UnitTests.Api.Contracts;

/// <summary>
/// Fast, HTTP-free unit tests for the data-annotation validation rules on
/// <see cref="CreateLeadRequest"/>. These exercise the API-level ("is this request
/// structurally valid?") tier only, per docs/architecture.md section 12 — no business rules,
/// no Application/Domain layer involved.
/// </summary>
public class CreateLeadRequestValidationTests
{
    private static CreateLeadRequest ValidRequest() => new()
    {
        Name = "Jane Accountant",
        Email = "jane@example.com",
        Inquiry = "I need help with catch-up bookkeeping for my small business.",
    };

    private static IList<ValidationResult> Validate(CreateLeadRequest request)
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    private static bool HasErrorFor(IList<ValidationResult> results, string memberName) =>
        results.Any(r => r.MemberNames.Contains(memberName));

    // ---- Happy path -------------------------------------------------------

    [Fact]
    public void Validate_WhenAllMinimumRequiredFieldsProvided_ProducesNoErrors()
    {
        var request = ValidRequest();

        var results = Validate(request);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenAllFieldsIncludingOptionalOnesProvided_ProducesNoErrors()
    {
        var request = ValidRequest();
        request.Phone = "+1 555 123 4567";
        request.CompanyName = "Acme Bookkeeping LLC";
        request.ClientType = ClientType.SoleProprietorship;
        request.ApproximateAnnualRevenue = 250_000m;
        request.AccountingSoftware = "QuickBooks";

        var results = Validate(request);

        Assert.Empty(results);
    }

    // ---- Name ---------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_WhenNameMissing_ProducesRequiredError(string? name)
    {
        var request = ValidRequest();
        request.Name = name!;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Name)));
    }

    [Fact]
    public void Validate_WhenNameIsWhitespaceOnly_ProducesRequiredError()
    {
        // RequiredAttribute trims string values by default (AllowEmptyStrings = false),
        // so a whitespace-only value is treated the same as a missing value.
        var request = ValidRequest();
        request.Name = "   ";

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Name)));
    }

    [Fact]
    public void Validate_WhenNameIsExactlyAtMaxLength_ProducesNoError()
    {
        var request = ValidRequest();
        request.Name = new string('N', 150);

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.Name)));
    }

    [Fact]
    public void Validate_WhenNameExceedsMaxLength_ProducesLengthError()
    {
        var request = ValidRequest();
        request.Name = new string('N', 151);

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Name)));
    }

    // ---- Email ----------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_WhenEmailMissing_ProducesRequiredError(string? email)
    {
        var request = ValidRequest();
        request.Email = email!;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Email)));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.example.com")]
    [InlineData("double@@example.com")]
    public void Validate_WhenEmailIsMalformed_ProducesFormatError(string email)
    {
        var request = ValidRequest();
        request.Email = email;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Email)));
    }

    [Fact]
    public void Validate_WhenEmailIsWhitespaceOnly_ProducesRequiredError()
    {
        var request = ValidRequest();
        request.Email = "   ";

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Email)));
    }

    [Fact]
    public void Validate_WhenEmailIsExactlyAtMaxLength_ProducesNoError()
    {
        // "local@" (6) + domain long enough to reach exactly 254 total, valid format.
        var local = "user@";
        var domain = new string('a', 254 - local.Length - 4) + ".com";
        var email = local + domain;
        Assert.Equal(254, email.Length);

        var request = ValidRequest();
        request.Email = email;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.Email)));
    }

    [Fact]
    public void Validate_WhenEmailExceedsMaxLength_ProducesLengthError()
    {
        var local = "user@";
        var domain = new string('a', 255 - local.Length - 4) + ".com";
        var email = local + domain;
        Assert.Equal(255, email.Length);

        var request = ValidRequest();
        request.Email = email;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Email)));
    }

    // ---- Inquiry ----------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_WhenInquiryMissing_ProducesRequiredError(string? inquiry)
    {
        var request = ValidRequest();
        request.Inquiry = inquiry!;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Inquiry)));
    }

    [Fact]
    public void Validate_WhenInquiryIsWhitespaceOnly_ProducesRequiredError()
    {
        var request = ValidRequest();
        request.Inquiry = "   ";

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Inquiry)));
    }

    [Fact]
    public void Validate_WhenInquiryIsExactlyAtMaxLength_ProducesNoError()
    {
        var request = ValidRequest();
        request.Inquiry = new string('I', 4000);

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.Inquiry)));
    }

    [Fact]
    public void Validate_WhenInquiryExceedsMaxLength_ProducesLengthError()
    {
        var request = ValidRequest();
        request.Inquiry = new string('I', 4001);

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Inquiry)));
    }

    // ---- Phone (optional) ---------------------------------------------------

    [Fact]
    public void Validate_WhenPhoneOmitted_ProducesNoError()
    {
        var request = ValidRequest();
        request.Phone = null;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.Phone)));
    }

    [Fact]
    public void Validate_WhenPhoneIsEmptyString_ProducesNoError()
    {
        // Phone is optional (no [Required]), so an empty string — distinct from
        // omitting the field entirely — must still pass validation.
        var request = ValidRequest();
        request.Phone = string.Empty;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.Phone)));
    }

    [Fact]
    public void Validate_WhenPhoneIsExactlyAtMaxLength_ProducesNoError()
    {
        var request = ValidRequest();
        request.Phone = new string('5', 50);

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.Phone)));
    }

    [Fact]
    public void Validate_WhenPhoneExceedsMaxLength_ProducesLengthError()
    {
        var request = ValidRequest();
        request.Phone = new string('5', 51);

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.Phone)));
    }

    // ---- CompanyName (optional) -------------------------------------------

    [Fact]
    public void Validate_WhenCompanyNameOmitted_ProducesNoError()
    {
        var request = ValidRequest();
        request.CompanyName = null;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.CompanyName)));
    }

    [Fact]
    public void Validate_WhenCompanyNameIsEmptyString_ProducesNoError()
    {
        var request = ValidRequest();
        request.CompanyName = string.Empty;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.CompanyName)));
    }

    [Fact]
    public void Validate_WhenCompanyNameIsExactlyAtMaxLength_ProducesNoError()
    {
        var request = ValidRequest();
        request.CompanyName = new string('C', 200);

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.CompanyName)));
    }

    [Fact]
    public void Validate_WhenCompanyNameExceedsMaxLength_ProducesLengthError()
    {
        var request = ValidRequest();
        request.CompanyName = new string('C', 201);

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.CompanyName)));
    }

    // ---- AccountingSoftware (optional) -------------------------------------

    [Fact]
    public void Validate_WhenAccountingSoftwareOmitted_ProducesNoError()
    {
        var request = ValidRequest();
        request.AccountingSoftware = null;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.AccountingSoftware)));
    }

    [Fact]
    public void Validate_WhenAccountingSoftwareIsEmptyString_ProducesNoError()
    {
        var request = ValidRequest();
        request.AccountingSoftware = string.Empty;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.AccountingSoftware)));
    }

    [Fact]
    public void Validate_WhenAccountingSoftwareIsExactlyAtMaxLength_ProducesNoError()
    {
        var request = ValidRequest();
        request.AccountingSoftware = new string('S', 100);

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.AccountingSoftware)));
    }

    [Fact]
    public void Validate_WhenAccountingSoftwareExceedsMaxLength_ProducesLengthError()
    {
        var request = ValidRequest();
        request.AccountingSoftware = new string('S', 101);

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.AccountingSoftware)));
    }

    // ---- ClientType (optional enum, validated via [EnumDataType]) ------------

    [Fact]
    public void Validate_WhenClientTypeOmitted_ProducesNoError()
    {
        var request = ValidRequest();
        request.ClientType = null;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.ClientType)));
    }

    [Theory]
    [InlineData(ClientType.Individual)]
    [InlineData(ClientType.SoleProprietorship)]
    [InlineData(ClientType.Corporation)]
    [InlineData(ClientType.LimitedLiabilityCompany)]
    [InlineData(ClientType.Partnership)]
    [InlineData(ClientType.NonProfit)]
    [InlineData(ClientType.Other)]
    public void Validate_WhenClientTypeIsDefinedEnumValue_ProducesNoError(ClientType clientType)
    {
        var request = ValidRequest();
        request.ClientType = clientType;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.ClientType)));
    }

    [Fact]
    public void Validate_WhenClientTypeIsUndefinedEnumValue_ProducesEnumDataTypeError()
    {
        // Not reachable through normal C# enum syntax — only via a cast — but this is exactly
        // the case [EnumDataType] exists to catch (e.g. a raw out-of-range integer that
        // deserializes into the property without going through Enum.Parse).
        var request = ValidRequest();
        request.ClientType = (ClientType)99;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.ClientType)));
    }

    // ---- ApproximateAnnualRevenue (optional, must be non-negative) --------

    [Fact]
    public void Validate_WhenApproximateAnnualRevenueOmitted_ProducesNoError()
    {
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = null;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.ApproximateAnnualRevenue)));
    }

    [Fact]
    public void Validate_WhenApproximateAnnualRevenueIsZero_ProducesNoError()
    {
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = 0m;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.ApproximateAnnualRevenue)));
    }

    [Fact]
    public void Validate_WhenApproximateAnnualRevenueIsPositive_ProducesNoError()
    {
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = 1_000_000.50m;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.ApproximateAnnualRevenue)));
    }

    [Fact]
    public void Validate_WhenApproximateAnnualRevenueIsNegative_ProducesRangeError()
    {
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = -0.01m;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.ApproximateAnnualRevenue)));
    }

    [Fact]
    public void Validate_WhenApproximateAnnualRevenueIsLargeNegative_ProducesRangeError()
    {
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = -1_000_000m;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.ApproximateAnnualRevenue)));
    }

    [Fact]
    public void Validate_WhenApproximateAnnualRevenueIsExactlyAtMaxLiteral_ProducesNoError()
    {
        // Upper bound of the [Range] attribute is Lead.MaxApproximateAnnualRevenueLiteral,
        // which matches the decimal(18,2) storage column's real capacity
        // (see LeadConfiguration in Infrastructure) rather than decimal.MaxValue.
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = Lead.MaxApproximateAnnualRevenue;

        var results = Validate(request);

        Assert.False(HasErrorFor(results, nameof(CreateLeadRequest.ApproximateAnnualRevenue)));
    }

    [Fact]
    public void Validate_WhenApproximateAnnualRevenueExceedsMaxLiteral_ProducesRangeError()
    {
        // Regression test for the bug where [Range] allowed values up to decimal.MaxValue
        // (~7.9e28) even though the decimal(18,2) storage column can only hold up to
        // 9999999999999999.99. 10_000_000_000_000_000 (1e16) is one cent over that ceiling and
        // is the exact repro value that previously passed both API and Domain validation but
        // threw at SaveChangesAsync time.
        var request = ValidRequest();
        request.ApproximateAnnualRevenue = 10_000_000_000_000_000m;

        var results = Validate(request);

        Assert.True(HasErrorFor(results, nameof(CreateLeadRequest.ApproximateAnnualRevenue)));
    }
}
