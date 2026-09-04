using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain;
using Ordering.Infrastructure;
using SharedKernel;

namespace OrderFulfillment.Api;

/// Drains Ordering's outbox and routes each event through the same
/// dispatcher a real Service Bus subscriber would call — see
/// InProcessEventDispatcher for why this in-process hop stands in for a
/// network one. A BackgroundService here plays the same role as
/// OutboxPattern-Demo's separate relay process; it's a background loop, not
/// a queue-drain, so it doesn't need Channel<T> — see BackgroundQueue-Demo
/// for when that pattern is the right one instead.
public class OutboxDispatcherBackgroundService(
    IServiceProvider services,
    ILogger<OutboxDispatcherBackgroundService> logger) : BackgroundService
{
    private static readonly Dictionary<string, Type> EventTypesByName = new()
    {
        [nameof(OrderPlaced)] = typeof(OrderPlaced),
        [nameof(OrderConfirmed)] = typeof(OrderConfirmed),
        [nameof(OrderCancelled)] = typeof(OrderCancelled),
        [nameof(OrderFulfilled)] = typeof(OrderFulfilled),
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchPendingAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
        }
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        // Loops because dispatching one event (e.g. OrderPlaced -> Ordering
        // confirms) can itself write a NEW outbox row (OrderConfirmed) — a
        // single pass would leave that one for the next poll instead of
        // draining the whole chain now.
        while (await DispatchOneBatchAsync(cancellationToken) > 0)
        {
        }
    }

    private async Task<int> DispatchOneBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<InProcessEventDispatcher>();

        // SQLite's EF Core provider can't translate ORDER BY on a
        // DateTimeOffset column — order client-side after materializing
        // (same fix as OutboxPattern-Demo).
        var pending = (await db.OutboxMessages.Where(m => m.ProcessedAt == null).ToListAsync(cancellationToken))
            .OrderBy(m => m.OccurredAt)
            .ToList();

        foreach (var message in pending)
        {
            if (!EventTypesByName.TryGetValue(message.Type, out var eventType))
            {
                logger.LogWarning("No known event type for outbox message {Type}", message.Type);
                continue;
            }

            var @event = (IntegrationEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
            await dispatcher.PublishAsync(@event, cancellationToken);

            message.MarkProcessed();
            await db.SaveChangesAsync(cancellationToken);
        }

        return pending.Count;
    }
}
