using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AccountingLeads.UnitTests.Application.TestDoubles;

/// <summary>
/// Minimal hand-rolled <see cref="DbSet{TEntity}"/> stand-in used only so
/// <c>CreateLeadCommandHandler</c> can be exercised against
/// <c>IApplicationDbContext</c> without a real EF Core provider (no InMemory/SqlServer
/// package is referenced by this test project). Most of <see cref="DbSet{TEntity}"/>'s
/// members are virtual (not abstract) and throw <see cref="InvalidOperationException"/> by
/// default ("custom" DbSets are not supported without a provider) — this subclass only
/// overrides the single member the handler actually calls, <see cref="Add"/>, recording
/// entities in a plain in-memory list, plus the one genuinely abstract member
/// (<see cref="EntityType"/>) which must be implemented to satisfy the compiler but is never
/// exercised by the handler under test. Any other member (querying, Remove, etc.) falls back
/// to the base implementation and throws, which is fine because the handler under test
/// doesn't use them.
/// </summary>
internal sealed class FakeDbSet<TEntity> : DbSet<TEntity>
    where TEntity : class
{
    private readonly List<TEntity> _items = new();

    public IReadOnlyList<TEntity> Items => _items;

    public override IEntityType EntityType =>
        throw new NotSupportedException($"{nameof(EntityType)} is not used by {nameof(FakeDbSet<TEntity>)}.");

    public override EntityEntry<TEntity> Add(TEntity entity)
    {
        _items.Add(entity);
        return null!;
    }
}
