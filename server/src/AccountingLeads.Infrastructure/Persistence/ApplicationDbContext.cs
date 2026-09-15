using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace AccountingLeads.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<LeadAnalysis> LeadAnalyses => Set<LeadAnalysis>();
    public DbSet<LeadQualificationResult> LeadQualificationResults => Set<LeadQualificationResult>();

    public async Task<IReadOnlyList<OutboxMessage>> GetUnpublishedOutboxMessagesAsync(int maxCount, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return await OutboxMessages
            .Where(m => m.PublishedAtUtc == null && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now))
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        await Leads.SingleOrDefaultAsync(l => l.Id == leadId, cancellationToken);

    public async Task<LeadAnalysis?> GetLeadAnalysisByLeadIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        await LeadAnalyses.SingleOrDefaultAsync(a => a.LeadId == leadId, cancellationToken);

    public async Task<LeadQualificationResult?> GetLeadQualificationResultByLeadIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        await LeadQualificationResults.SingleOrDefaultAsync(r => r.LeadId == leadId, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
