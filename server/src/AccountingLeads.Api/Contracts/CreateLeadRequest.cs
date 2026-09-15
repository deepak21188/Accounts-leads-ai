using System.ComponentModel.DataAnnotations;
using AccountingLeads.Domain.Leads;

namespace AccountingLeads.Api.Contracts;

public sealed class CreateLeadRequest
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(254)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [EnumDataType(typeof(ClientType))]
    public ClientType? ClientType { get; set; }

    [Range(typeof(decimal), "0", Lead.MaxApproximateAnnualRevenueLiteral)]
    public decimal? ApproximateAnnualRevenue { get; set; }

    [StringLength(100)]
    public string? AccountingSoftware { get; set; }

    [Required]
    [StringLength(4000)]
    public string Inquiry { get; set; } = string.Empty;
}
