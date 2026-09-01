using Azure.Messaging.ServiceBus;

namespace ServiceBusDemo;

/// One subscription on the topic, one listener — every message published to
/// the topic reaches this independently of whatever the processing-sub
/// competing consumers are doing with their own copy of the same messages.
public sealed class AuditSubscriber(ServiceBusClient client)
{
    private ServiceBusProcessor? _processor;

    public async Task StartAsync()
    {
        _processor = client.CreateProcessor("orders", "audit-sub", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
        });

        _processor.ProcessMessageAsync += async args =>
        {
            Console.WriteLine($"[audit] saw message {args.Message.MessageId}: \"{args.Message.Body}\"");
            await args.CompleteMessageAsync(args.Message);
        };

        _processor.ProcessErrorAsync += args =>
        {
            Console.WriteLine($"[audit] error: {args.Exception.Message}");
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync();
    }

    public Task StopAsync() => _processor?.StopProcessingAsync() ?? Task.CompletedTask;
}
