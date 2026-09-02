namespace OutboxPatternDemo;

/// The consumer's own durable idempotency ledger — persisted, not in-memory,
/// because the whole point of this demo is proving safety across process
/// restarts, and an in-memory dedup set would reset exactly when it matters.
public class ConsumedMessage
{
    public string MessageId { get; private set; } = string.Empty;
    public DateTimeOffset ConsumedAt { get; private set; } = DateTimeOffset.UtcNow;

    private ConsumedMessage() { }

    public static ConsumedMessage For(string messageId) => new() { MessageId = messageId };
}
