using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OutboxPatternDemo;

public class OutboxRelay(AppDbContext db, ServiceBusSender sender, ILogger<OutboxRelay> logger)
{
    /// Publishes every unsent outbox row, marking each one processed
    /// immediately after its own publish — not batched at the end — so a
    /// crash only ever puts the ONE message currently in flight at risk of
    /// re-delivery, never messages already marked sent earlier in this run.
    public async Task<int> RelayOnceAsync(bool simulateCrashAfterPublish, CancellationToken ct = default)
    {
        // SQLite's EF Core provider can't translate ORDER BY on a
        // DateTimeOffset column into SQL — order client-side instead.
        var pending = (await db.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .ToListAsync(ct))
            .OrderBy(m => m.OccurredAt)
            .ToList();

        var publishedCount = 0;

        foreach (var message in pending)
        {
            var busMessage = new ServiceBusMessage(message.Payload)
            {
                MessageId = message.Id.ToString(), // the outbox row's own id IS the idempotency key downstream
                Subject = message.Type,
            };

            await sender.SendMessageAsync(busMessage, ct);
            publishedCount++;
            logger.LogInformation("Published outbox message {Id} ({Type})", message.Id, message.Type);

            if (simulateCrashAfterPublish)
            {
                // The exact window a real crash could land in: publish
                // succeeded, but the row is still unprocessed in the DB when
                // the process dies here. On restart the relay will publish it
                // again — a duplicate, never a loss, because the row survived.
                throw new SimulatedCrashException(message.Id);
            }

            message.MarkProcessed();
            await db.SaveChangesAsync(ct);
        }

        return publishedCount;
    }
}

public sealed class SimulatedCrashException(Guid outboxMessageId)
    : Exception($"Simulated crash right after publishing {outboxMessageId}, before marking it sent.");
