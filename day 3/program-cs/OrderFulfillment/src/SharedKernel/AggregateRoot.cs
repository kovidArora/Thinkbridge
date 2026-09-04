using System.ComponentModel.DataAnnotations.Schema;

namespace SharedKernel;

/// Base for the one entity per module allowed to be loaded/saved directly
/// (the aggregate root) and the only thing allowed to raise integration
/// events — a module's other entities are only ever reached through it.
public abstract class AggregateRoot
{
    private readonly List<IntegrationEvent> _pendingEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    // EF Core's convention-based discovery otherwise treats this as a
    // navigation property and tries to map IntegrationEvent (abstract, no
    // concrete mapped type) as an entity type, which fails outright.
    [NotMapped]
    public IReadOnlyList<IntegrationEvent> PendingEvents => _pendingEvents;

    protected void Raise(IntegrationEvent @event) => _pendingEvents.Add(@event);

    /// Called by infrastructure right after SaveChanges writes these events
    /// to the module's own outbox table (same durability guarantee as
    /// OutboxPattern-Demo, applied per-module here).
    public void ClearPendingEvents() => _pendingEvents.Clear();
}
