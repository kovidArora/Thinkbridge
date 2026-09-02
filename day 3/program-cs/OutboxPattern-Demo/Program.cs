using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OutboxPatternDemo;

const string serviceBusConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
const string queueName = "outbox-relay";

var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=outbox-demo.db")
    .Options;

await using var db = new AppDbContext(dbOptions);
await db.Database.EnsureCreatedAsync();

using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));

switch (args.ElementAtOrDefault(0))
{
    case "place":
    {
        var orderService = new OrderService(db);
        var name = args.ElementAtOrDefault(1) ?? "kovid";
        var amount = decimal.Parse(args.ElementAtOrDefault(2) ?? "42.00");
        var id = await orderService.PlaceOrderAsync(name, amount);
        Console.WriteLine($"Placed order {id} — domain row + outbox row written in one transaction.");
        break;
    }

    case "relay":
    {
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        await using var sender = client.CreateSender(queueName);
        var relay = new OutboxRelay(db, sender, loggerFactory.CreateLogger<OutboxRelay>());

        try
        {
            var count = await relay.RelayOnceAsync(simulateCrashAfterPublish: args.Contains("--crash"));
            Console.WriteLine($"Relay published {count} message(s), all marked sent.");
        }
        catch (SimulatedCrashException ex)
        {
            Console.WriteLine($"*** {ex.Message} ***");
            Console.WriteLine("Process terminating uncleanly now. The row is still unprocessed in the DB.");
            Environment.Exit(1);
        }
        break;
    }

    case "consume":
    {
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        await using var receiver = client.CreateReceiver(queueName);
        var consumer = new OutboxConsumer(db, receiver, loggerFactory.CreateLogger<OutboxConsumer>());
        var (handled, duplicates) = await consumer.DrainOnceAsync();
        Console.WriteLine($"Consumed: {handled} handled, {duplicates} duplicate delivery(ies) safely skipped.");
        break;
    }

    case "status":
    {
        Console.WriteLine(
            $"Orders={await db.Orders.CountAsync()} " +
            $"OutboxRows={await db.OutboxMessages.CountAsync()} " +
            $"Unsent={await db.OutboxMessages.CountAsync(m => m.ProcessedAt == null)} " +
            $"DistinctConsumed={await db.ConsumedMessages.CountAsync()}");
        break;
    }

    default:
        Console.WriteLine("Usage: place <name> <amount> | relay [--crash] | consume | status");
        break;
}
