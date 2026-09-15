using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Persistence;
using AccountingLeads.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace AccountingLeads.UnitTests.Application.TestDoubles;

/// <summary>
/// An <see cref="IApplicationDbContext"/> test double that throws if any member is touched.
/// Used to prove that <c>CreateLeadCommandHandler</c> short-circuits on validation failure
/// (invalid client type, or a <c>DomainValidationException</c> from <c>Lead.Create</c>)
/// without ever reading/writing the DbSets or calling <c>SaveChangesAsync</c>.
/// </summary>
internal sealed class ThrowingApplicationDbContext : IApplicationDbContext
{
    public DbSet<Lead> Leads =>
        throw new InvalidOperationException("Leads should not be accessed on a validation-failure path.");

    public DbSet<OutboxMessage> OutboxMessages =>
        throw new InvalidOperationException("OutboxMessages should not be accessed on a validation-failure path.");

    public DbSet<LeadAnalysis> LeadAnalyses =>
        throw new InvalidOperationException("LeadAnalyses should not be accessed on a validation-failure path.");

    public DbSet<LeadQualificationResult> LeadQualificationResults =>
        throw new InvalidOperationException("LeadQualificationResults should not be accessed on a validation-failure path.");

    public Task<IReadOnlyList<OutboxMessage>> GetUnpublishedOutboxMessagesAsync(int maxCount, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("GetUnpublishedOutboxMessagesAsync should not be called on a validation-failure path.");

    public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("GetLeadByIdAsync should not be called on a validation-failure path.");

    public Task<LeadAnalysis?> GetLeadAnalysisByLeadIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("GetLeadAnalysisByLeadIdAsync should not be called on a validation-failure path.");

    public Task<LeadQualificationResult?> GetLeadQualificationResultByLeadIdAsync(Guid leadId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("GetLeadQualificationResultByLeadIdAsync should not be called on a validation-failure path.");

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("SaveChangesAsync should not be called on a validation-failure path.");
}
