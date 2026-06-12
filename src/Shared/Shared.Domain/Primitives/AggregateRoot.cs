namespace Shared.Domain.Primitives;

/// <summary>
/// Aggregate root — consistency boundary in DDD.
/// Exposes domain events collected during state transitions.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id) : base(id) { }
    protected AggregateRoot() { }

    /// <summary>Optimistic-concurrency token managed by EF Core.</summary>
    public uint RowVersion { get; private set; }
}
