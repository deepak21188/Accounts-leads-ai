using AccountingLeads.Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingLeads.Infrastructure.Persistence.Configurations;

public sealed class LeadQualificationResultConfiguration : IEntityTypeConfiguration<LeadQualificationResult>
{
    public void Configure(EntityTypeBuilder<LeadQualificationResult> builder)
    {
        builder.ToTable("LeadQualificationResults");

        builder.HasKey(result => result.Id);

        // One qualification result per lead — qualification only ever runs once per lead today,
        // same rationale as LeadAnalysis's unique index on LeadId.
        builder.HasIndex(result => result.LeadId).IsUnique();

        builder.Property(result => result.Score)
            .IsRequired();

        builder.Property(result => result.Priority)
            .IsRequired();

        // Primitive collection of strings, mapped to a JSON array column by EF Core's own
        // built-in convention — same pattern as LeadAnalysis.RequestedServices.
        builder.PrimitiveCollection(result => result.Reasons)
            .IsRequired();

        builder.Property(result => result.RulesVersionApplied)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(result => result.CreatedAtUtc)
            .IsRequired();
    }
}
