using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace AccountingLeads.UnitTests.Application.TestDoubles;

/// <summary>
/// In-memory <see cref="IApplicationDbContext"/> test double for the success path of
/// <c>CreateLeadCommandHandler</c>. Records what was added and how many times
/// <see cref="SaveChangesAsync"/> was called, without depending on a real EF Core provider.
/// </summary>
internal sealed class FakeApplicationDbContext : IApplicationDbContext
{
    private readonly FakeDbSet<Lead> _leads = new();
    private readonly FakeDbSet<OutboxMessage> _outboxMessages = new();
    private readonly FakeDbSet<LeadAnalysis> _leadAnalyses = new();
    private readonly FakeDbSet<LeadQualificationResult> _leadQualificationResults = new();

    public DbSet<Lead> Leads => _leads;

    public DbSet<OutboxMessage> OutboxMessages => _outboxMessages;

    public DbSet<LeadAnalysis> LeadAnalyses => _leadAnalyses;

    public DbSet<LeadQualificationResult> LeadQualificationResults => _leadQualificationResults;

    public IReadOnlyList<Lead> AddedLeads => _leads.Items;

    public IReadOnlyList<OutboxMessage> AddedOutboxMessages => _outboxMessages.Items;

    public IReadOnlyList<LeadAnalysis> AddedLeadAnalyses => _leadAnalyses.Items;

    public IReadOnlyList<LeadQualificationResult> AddedLeadQualificationResults => _leadQualificationResults.Items;

    public int SaveChangesCallCount { get; private set; }

    public bool ThrowOnNextSaveChanges { get; set; }

    public Task<IReadOnlyList<OutboxMessage>> GetUnpublishedOutboxMessagesAsync(int maxCount, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        IReadOnlyList<OutboxMessage> result = _outboxMessages.Items
            .Where(m => m.PublishedAtUtc is null && (m.NextAttemptAtUtc is null || m.NextAttemptAtUtc <= now))
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .Take(maxCount)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        Task.FromResult(_leads.Items.SingleOrDefault(l => l.Id == leadId));

    public Task<LeadAnalysis?> GetLeadAnalysisByLeadIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        Task.FromResult(_leadAnalyses.Items.SingleOrDefault(a => a.LeadId == leadId));

    public Task<LeadQualificationResult?> GetLeadQualificationResultByLeadIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        Task.FromResult(_leadQualificationResults.Items.SingleOrDefault(r => r.LeadId == leadId));

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ThrowOnNextSaveChanges)
        {
            ThrowOnNextSaveChanges = false;
            throw new InvalidOperationException("Simulated SaveChanges failure.");
        }

        SaveChangesCallCount++;
        return Task.FromResult(_leads.Items.Count + _outboxMessages.Items.Count + _leadAnalyses.Items.Count + _leadQualificationResults.Items.Count);
    }
}
