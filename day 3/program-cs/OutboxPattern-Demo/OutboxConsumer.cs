using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OutboxPatternDemo;

public class OutboxConsumer(AppDbContext db, ServiceBusReceiver receiver, ILogger<OutboxConsumer> logger)
{
    public async Task<(int Handled, int Duplicates)> DrainOnceAsync(CancellationToken ct = default)
    {
        int handled = 0, duplicates = 0;

        while (true)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(3), ct);
            if (message is null)
            {
                break;
            }

            var alreadyConsumed = await db.ConsumedMessages.AnyAsync(c => c.MessageId == message.MessageId, ct);
            if (alreadyConsumed)
            {
                logger.LogInformation("Duplicate delivery of {Id} — already handled, skipping.", message.MessageId);
                duplicates++;
            }
            else
            {
                logger.LogInformation("Handling {Id}: {Body}", message.MessageId, message.Body);
                db.ConsumedMessages.Add(ConsumedMessage.For(message.MessageId));
                await db.SaveChangesAsync(ct);
                handled++;
            }

            await receiver.CompleteMessageAsync(message, ct);
        }

        return (handled, duplicates);
    }
}
