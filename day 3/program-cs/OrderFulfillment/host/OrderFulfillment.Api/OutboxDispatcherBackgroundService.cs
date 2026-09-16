using System.Diagnostics;
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
    // Name registered with the OpenTelemetry tracer in Program.cs
    // (AddSource) — a BackgroundService has no ambient Activity of its own
    // the way an HTTP request does, so spans have to be started explicitly.
    public static readonly ActivitySource ActivitySource = new("OrderFulfillment.Worker");

    // maps the event's name-as-text (what's stored in the db) back to its
    // real C# class, so json can be turned back into an actual object
    private static readonly Dictionary<string, Type> EventTypesByName = new()
    {
        [nameof(OrderPlaced)] = typeof(OrderPlaced),
        [nameof(OrderConfirmed)] = typeof(OrderConfirmed),
        [nameof(OrderCancelled)] = typeof(OrderCancelled),
        [nameof(OrderFulfilled)] = typeof(OrderFulfilled),
    };

    // run dispatchpending 
    //wait 200 ms
    //repeat until cancelled
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchPendingAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
        }
    }

    // keep draining one batch at a time — dispatching one event can create a
    // brand new outbox row (e.g. OrderPlaced -> StockReserved), so one pass
    // isn't enough to clear a whole chain in one go
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

    // grab every unprocessed outbox row, oldest first, and for each one:
    // find its real type, start a span, turn the json back into a real
    // event object, dispatch it, then mark it processed
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
            // message.Type is just text ("OrderPlaced") — look up the real
            // class behind that name so Deserialize knows what to build
            if (!EventTypesByName.TryGetValue(message.Type, out var eventType))
            {
                logger.LogWarning("No known event type for outbox message {Type}", message.Type);
                continue;
            }

            // Resumes the trace the original HTTP request started (see
            // OutboxMessage.TraceParent) instead of starting an unrelated
            // one — this span, and every DB/dependency call nested under it
            // via dispatcher.
            // Async, shows up as part of that same
            // end-to-end trace in Application Insights.
            using var activity = StartDispatchActivity(message);

            try
            {
                var @event = (IntegrationEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
                await dispatcher.PublishAsync(@event, cancellationToken);

                message.MarkProcessed();
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Isolate this one message's failure. Without this, an
                // unhandled exception here propagates out of ExecuteAsync,
                // and since .NET 6 the default BackgroundServiceException
                // Behavior is StopHost -- one bad message would otherwise
                // crash the entire application, not just skip itself.
                // Left unprocessed on purpose: the next poll retries it,
                // same at-least-once semantics as a real broker's
                // redelivery. A message that can never succeed will retry
                // forever with no dead-letter equivalent yet -- that's
                // real follow-up work, not solved by this change.
                logger.LogError(
                    ex,
                    "Failed to dispatch outbox message {MessageId} ({Type}); will retry on next poll",
                    message.Id,
                    message.Type);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
        }

        return pending.Count;
    }

    // if this row has a saved traceparent, resume that trace instead of
    // starting a new unrelated one — this is what links a request's span to
    // this later, async span in the same trace
    private static Activity? StartDispatchActivity(OutboxMessage message)
    {
        var parentContext = !string.IsNullOrEmpty(message.TraceParent)
            && ActivityContext.TryParse(message.TraceParent, traceState: null, out var parsed)
                ? parsed
                : default;

        return ActivitySource.StartActivity(
            $"outbox.dispatch {message.Type}",
            ActivityKind.Consumer,
            parentContext);
    }
}
