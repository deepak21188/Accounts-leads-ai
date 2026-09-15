using AccountingLeads.Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingLeads.Infrastructure.Persistence.Configurations;

public sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");

        builder.HasKey(lead => lead.Id);

        builder.Property(lead => lead.Name)
            .HasMaxLength(Lead.NameMaxLength)
            .IsRequired();

        builder.Property(lead => lead.Email)
            .HasMaxLength(Lead.EmailMaxLength)
            .IsRequired();

        builder.Property(lead => lead.Phone)
            .HasMaxLength(Lead.PhoneMaxLength);

        builder.Property(lead => lead.CompanyName)
            .HasMaxLength(Lead.CompanyNameMaxLength);

        builder.Property(lead => lead.ClientType);

        // decimal(18,2) capacity must match Lead.MaxApproximateAnnualRevenue.
        builder.Property(lead => lead.ApproximateAnnualRevenue)
            .HasColumnType("decimal(18,2)");

        builder.Property(lead => lead.AccountingSoftware)
            .HasMaxLength(Lead.AccountingSoftwareMaxLength);

        builder.Property(lead => lead.Inquiry)
            .HasMaxLength(Lead.InquiryMaxLength)
            .IsRequired();

        builder.Property(lead => lead.LeadStatus)
            .IsRequired();

        builder.Property(lead => lead.AiProcessingStatus)
            .IsRequired();

        builder.Property(lead => lead.CreatedAtUtc)
            .IsRequired();
    }
}
