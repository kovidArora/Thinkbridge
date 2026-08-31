using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BackgroundQueueDemo;

public sealed class QueuedHostedService(IBackgroundTaskQueue queue, ILogger<QueuedHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Queue drain loop starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Func<CancellationToken, Task> workItem;
            try
            {
                // Blocks here when the queue is empty. Cancelling stoppingToken while
                // waiting throws instead of returning a bogus item, so the catch below
                // is what actually ends the loop on shutdown — the while condition
                // alone would never see IsCancellationRequested flip while parked here.
                workItem = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                // Passing stoppingToken through lets a long-running item cut itself
                // short cooperatively; it's the item's job to actually check/pass it on.
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background work item threw.");
            }
        }

        logger.LogInformation("Queue drain loop exiting.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Shutdown requested — waiting for the current work item to finish.");
        // Base implementation awaits ExecuteAsync's Task (bounded by the host's
        // shutdown timeout, HostOptions.ShutdownTimeout, default 30s) rather than
        // killing it outright.
        await base.StopAsync(cancellationToken);
    }
}
