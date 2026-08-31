using System.Threading.Channels;

namespace BackgroundQueueDemo;

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _channel;

    public BackgroundTaskQueue(int capacity)
    {
        // Bounded + Wait: a producer (e.g. an API endpoint) backs off instead of
        // unboundedly growing memory if consumers fall behind.
        _channel = Channel.CreateBounded<Func<CancellationToken, Task>>(
            new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait });
    }

    public async ValueTask QueueAsync(Func<CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _channel.Writer.WriteAsync(workItem);
    }

    public ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
