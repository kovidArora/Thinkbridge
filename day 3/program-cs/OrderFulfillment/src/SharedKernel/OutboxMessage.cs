using System.Diagnostics;
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

    /// The W3C traceparent of whatever request/activity was live when this
    /// row was written (Activity.Current?.Id is already in that exact
    /// format). Without this, the background dispatcher's later processing
    /// would start its own unrelated trace — this is what lets it instead
    /// resume the SAME trace the original HTTP request started, even though
    /// the two run on different threads at different times.
    public string? TraceParent { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage From(IntegrationEvent @event) => new()
    {
        Id = @event.Id,
        Type = @event.GetType().Name,
        Payload = JsonSerializer.Serialize(@event, @event.GetType()),
        OccurredAt = @event.OccurredAt,
        TraceParent = Activity.Current?.Id,
    };

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;
}
