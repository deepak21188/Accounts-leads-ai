using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace AccountingLeads.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Lead> Leads { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<LeadAnalysis> LeadAnalyses { get; }
    DbSet<LeadQualificationResult> LeadQualificationResults { get; }

    Task<IReadOnlyList<OutboxMessage>> GetUnpublishedOutboxMessagesAsync(int maxCount, CancellationToken cancellationToken);
    Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken);
    Task<LeadAnalysis?> GetLeadAnalysisByLeadIdAsync(Guid leadId, CancellationToken cancellationToken);
    Task<LeadQualificationResult?> GetLeadQualificationResultByLeadIdAsync(Guid leadId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
