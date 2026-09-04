namespace SharedKernel;

/// The only thing modules are allowed to know about each other with: a
/// published fact about something that already happened, never a command.
/// Ordering doesn't know Inventory exists — it publishes OrderPlaced and
/// moves on; Inventory decides for itself whether that's interesting.
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
