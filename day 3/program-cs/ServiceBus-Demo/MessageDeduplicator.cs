using System.Collections.Concurrent;

namespace ServiceBusDemo;

/// Tracks which message ids a worker has already handled. Service Bus is
/// at-least-once delivery — a message can legitimately arrive twice (e.g. the
/// worker crashes after processing but before completing the message), so a
/// handler that isn't safe to run twice needs this kind of check.
public sealed class MessageDeduplicator
{
    private readonly ConcurrentDictionary<string, byte> _seen = new();

    /// True once a message id has SUCCEEDED — checked before doing any work.
    public bool HasSucceeded(string messageId) => _seen.ContainsKey(messageId);

    /// Call only after the handler has actually succeeded. Marking on attempt
    /// start instead of success would let a failed attempt (e.g. an exception
    /// mid-handler) permanently look "already handled" — the retry would then
    /// get skipped as a false duplicate instead of actually retrying, which
    /// would silently defeat both real retries and the dead-letter path.
    public void MarkSucceeded(string messageId) => _seen.TryAdd(messageId, 0);
}
