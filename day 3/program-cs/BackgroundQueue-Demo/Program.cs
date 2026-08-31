using BackgroundQueueDemo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(capacity: 50));
builder.Services.AddHostedService<QueuedHostedService>();

using var host = builder.Build();

// Stand-in for something like an API endpoint enqueuing work instead of doing it
// inline on the request thread. Jobs 1-4 finish fast; job 5 runs long enough that
// pressing Ctrl+C while it's mid-flight actually demonstrates graceful shutdown —
// the host waits for it (up to HostOptions.ShutdownTimeout) instead of killing it.
var queue = host.Services.GetRequiredService<IBackgroundTaskQueue>();
for (var i = 1; i <= 4; i++)
{
    var jobId = i;
    await queue.QueueAsync(async ct =>
    {
        Console.WriteLine($"[job {jobId}] starting");
        await Task.Delay(500, ct);
        Console.WriteLine($"[job {jobId}] done");
    });
}
await queue.QueueAsync(async ct =>
{
    Console.WriteLine("[job 5] starting (10s — try Ctrl+C now)");
    await Task.Delay(TimeSpan.FromSeconds(10), ct);
    Console.WriteLine("[job 5] done");
});

await host.RunAsync();
