using System.Text.Json;

namespace OutboxPatternDemo;

/// Written in the SAME transaction as the domain change it describes. Its
/// existence in the DB is the durable proof that "this event needs to be
/// published" — a separate relay reads unprocessed rows and publishes them,
/// so the domain write and the publish intent can never diverge even if the
/// process dies the instant after the transaction commits.
public class OutboxMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage For(string type, object payload) => new()
    {
        Type = type,
        Payload = JsonSerializer.Serialize(payload),
    };

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;
}
