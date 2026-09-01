using Azure.Messaging.ServiceBus;

namespace ServiceBusDemo;

/// Several independent processors bound to the SAME subscription — standing
/// in for several worker instances/processes. Service Bus doesn't care how
/// many receivers are attached; whichever one is free grabs the next message
/// and locks it, so they compete rather than each getting their own copy
/// (that fan-out behavior is what the topic's separate subscriptions give
/// you; this is what happens *within* one subscription).
public sealed class CompetingConsumerWorkers(ServiceBusClient client, MessageDeduplicator dedup, int workerCount)
{
    public const string PoisonBody = "POISON";

    private readonly List<ServiceBusProcessor> _processors = [];

    public async Task StartAsync()
    {
        for (var i = 1; i <= workerCount; i++)
        {
            var workerId = $"worker-{i}";
            var processor = client.CreateProcessor("orders", "processing-sub", new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 1,
            });

            processor.ProcessMessageAsync += args => HandleAsync(workerId, args);
            processor.ProcessErrorAsync += args =>
            {
                Console.WriteLine($"[{workerId}] transport error: {args.Exception.Message}");
                return Task.CompletedTask;
            };

            _processors.Add(processor);
            await processor.StartProcessingAsync();
        }
    }

    private async Task HandleAsync(string workerId, ProcessMessageEventArgs args)
    {
        var message = args.Message;

        if (dedup.HasSucceeded(message.MessageId))
        {
            Console.WriteLine($"[{workerId}] skipping duplicate delivery of {message.MessageId}");
            await args.CompleteMessageAsync(message);
            return;
        }

        if (message.Body.ToString() == PoisonBody)
        {
            // A real bug (bad data, a broken assumption) that will never
            // succeed no matter how many times it's retried. Not caught here
            // on purpose — letting it throw is what makes the SDK abandon the
            // message and, after DeliveryCount exceeds the subscription's
            // MaxDeliveryCount (3, see config.json), dead-letter it instead of
            // retrying forever. Crucially, it must NOT be marked as succeeded
            // here, or the retry would get skipped as a false duplicate.
            Console.WriteLine($"[{workerId}] processing {message.MessageId} (delivery #{message.DeliveryCount}) — about to blow up");
            throw new InvalidOperationException($"Cannot process message {message.MessageId}: poison payload.");
        }

        Console.WriteLine($"[{workerId}] processed {message.MessageId}: \"{message.Body}\"");
        dedup.MarkSucceeded(message.MessageId);
        await args.CompleteMessageAsync(message);
    }

    public async Task StopAsync()
    {
        foreach (var processor in _processors)
        {
            await processor.StopProcessingAsync();
        }
    }
}
