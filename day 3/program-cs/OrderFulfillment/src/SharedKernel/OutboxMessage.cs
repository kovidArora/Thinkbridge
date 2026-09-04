using System.Text.Json;

namespace SharedKernel;

/// Identical shape in every module's own database/schema — each module owns
/// its own outbox table rather than sharing one, so Ordering's reliability
/// never depends on Inventory's schema or uptime.
public class OutboxMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage From(IntegrationEvent @event) => new()
    {
        Id = @event.Id,
        Type = @event.GetType().Name,
        Payload = JsonSerializer.Serialize(@event, @event.GetType()),
        OccurredAt = @event.OccurredAt,
    };

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;
}
