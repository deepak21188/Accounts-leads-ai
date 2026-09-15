using AccountingLeads.Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingLeads.Infrastructure.Persistence.Configurations;

public sealed class LeadAnalysisConfiguration : IEntityTypeConfiguration<LeadAnalysis>
{
    public void Configure(EntityTypeBuilder<LeadAnalysis> builder)
    {
        builder.ToTable("LeadAnalyses");

        builder.HasKey(analysis => analysis.Id);

        // One analysis per lead in this slice — a redelivered-after-failure retry replaces
        // nothing yet (ProcessLeadCommandHandler only adds a new LeadAnalysis on the success
        // path, which only runs once per Lead since AiProcessingStatus.Completed short-circuits
        // any later delivery); the unique index makes that invariant explicit at the DB level.
        builder.HasIndex(analysis => analysis.LeadId).IsUnique();

        // Primitive collection of a closed enum, mapped to a JSON array column by EF Core's
        // own built-in convention (stable since EF Core 8) — not hand-rolled JSON, unlike
        // OutboxMessage.Payload's opaque envelope blob, since this is a homogeneous, strongly
        // typed collection EF already knows how to (de)serialize.
        builder.PrimitiveCollection(analysis => analysis.RequestedServices)
            .IsRequired();

        builder.Property(analysis => analysis.ExtractedClientType);

        // decimal(18,2) capacity must match Lead.MaxApproximateAnnualRevenue, same as
        // Lead.ApproximateAnnualRevenue.
        builder.Property(analysis => analysis.ExtractedApproximateAnnualRevenue)
            .HasColumnType("decimal(18,2)");

        builder.Property(analysis => analysis.ExtractedAccountingSoftware)
            .HasMaxLength(LeadAnalysis.ExtractedAccountingSoftwareMaxLength);

        builder.Property(analysis => analysis.Urgency);

        builder.Property(analysis => analysis.ExtractedFilingDeadline)
            .HasMaxLength(LeadAnalysis.ExtractedFilingDeadlineMaxLength);

        builder.Property(analysis => analysis.Notes)
            .HasMaxLength(LeadAnalysis.NotesMaxLength);

        builder.Property(analysis => analysis.EstimatedDeadlineInDays);

        builder.Property(analysis => analysis.BookkeepingMonthsBehind);

        builder.Property(analysis => analysis.CreatedAtUtc)
            .IsRequired();
    }
}
