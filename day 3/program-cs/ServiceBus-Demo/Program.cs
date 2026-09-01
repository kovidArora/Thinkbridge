using Azure.Messaging.ServiceBus;
using ServiceBusDemo;

const string connectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

await using var client = new ServiceBusClient(connectionString);

// --- Publish: one topic, two subscriptions fan out every message below to
// both audit-sub and processing-sub independently. ---
await using (var sender = client.CreateSender("orders"))
{
    await sender.SendMessageAsync(new ServiceBusMessage("first order") { MessageId = "order-1" });
    await sender.SendMessageAsync(new ServiceBusMessage("second order") { MessageId = "order-2" });
    await sender.SendMessageAsync(new ServiceBusMessage("third order") { MessageId = "order-3" });

    // A duplicate delivery of order-1 — same MessageId, standing in for
    // the kind of at-least-once redelivery Service Bus can legitimately do.
    Console.WriteLine("Publishing a duplicate of order-1...");
    await sender.SendMessageAsync(new ServiceBusMessage("first order") { MessageId = "order-1" });

    // A message no handler can ever succeed on.
    Console.WriteLine("Publishing a poison message...");
    await sender.SendMessageAsync(new ServiceBusMessage(CompetingConsumerWorkers.PoisonBody) { MessageId = "poison-1" });
}

var dedup = new MessageDeduplicator();
var audit = new AuditSubscriber(client);
var workers = new CompetingConsumerWorkers(client, dedup, workerCount: 3);

await audit.StartAsync();
await workers.StartAsync();

Console.WriteLine("Processing... (poison-1 will retry up to MaxDeliveryCount=3 before dead-lettering)");
await Task.Delay(TimeSpan.FromSeconds(10));

await audit.StopAsync();
await workers.StopAsync();

// --- Prove the dead-letter queue actually caught the poison message ---
await using var dlqReceiver = client.CreateReceiver("orders", "processing-sub", new ServiceBusReceiverOptions
{
    SubQueue = SubQueue.DeadLetter,
});

var deadLettered = await dlqReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
if (deadLettered is null)
{
    Console.WriteLine("Nothing in the dead-letter queue.");
}
else
{
    Console.WriteLine(
        $"Dead-letter queue caught: {deadLettered.MessageId} " +
        $"(reason: {deadLettered.DeadLetterReason}, delivery count: {deadLettered.DeliveryCount})");
    await dlqReceiver.CompleteMessageAsync(deadLettered);
}
