using System.Net.Http.Json;
using System.Text.Json;

var baseUrl = "http://localhost:5090";
using var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 500 });

async Task<long> GetDbQueryCountAsync()
{
    var body = await http.GetFromJsonAsync<JsonElement>($"{baseUrl}/api/debug/author-stats-metrics");
    return body.GetProperty("dbQueryCount").GetInt64();
}

async Task<TimeSpan> TimeOneRequestAsync(bool noCache)
{
    var url = noCache ? $"{baseUrl}/api/authors/stats?noCache=true" : $"{baseUrl}/api/authors/stats";
    var sw = System.Diagnostics.Stopwatch.StartNew();
    using var response = await http.GetAsync(url);
    response.EnsureSuccessStatusCode();
    sw.Stop();
    return sw.Elapsed;
}

async Task RunBurstAsync(string label, int concurrency, bool noCache)
{
    var before = await GetDbQueryCountAsync();
    var overall = System.Diagnostics.Stopwatch.StartNew();

    var tasks = Enumerable.Range(0, concurrency).Select(_ => TimeOneRequestAsync(noCache)).ToArray();
    var latencies = await Task.WhenAll(tasks);

    overall.Stop();
    var after = await GetDbQueryCountAsync();

    var sorted = latencies.OrderBy(t => t).ToArray();
    double PercentileMs(double p) => sorted[(int)Math.Ceiling(p * sorted.Length) - 1].TotalMilliseconds;

    Console.WriteLine($"--- {label} ---");
    Console.WriteLine($"  concurrency:      {concurrency}");
    Console.WriteLine($"  wall time:        {overall.Elapsed.TotalMilliseconds:F1} ms");
    Console.WriteLine($"  p50 latency:      {PercentileMs(0.50):F1} ms");
    Console.WriteLine($"  p99 latency:      {PercentileMs(0.99):F1} ms");
    Console.WriteLine($"  DB queries fired: {after - before}  (before={before}, after={after})");
    Console.WriteLine();
}

Console.WriteLine("=== Baseline: no cache, 50 concurrent requests (every request hits the DB) ===");
await RunBurstAsync("no-cache burst", concurrency: 50, noCache: true);

Console.WriteLine("=== Cold cache: evict key, then 50 concurrent requests (stampede protection test) ===");
await http.PostAsync($"{baseUrl}/api/debug/author-stats-cache/evict", content: null);
await RunBurstAsync("cold-cache burst", concurrency: 50, noCache: false);

Console.WriteLine("=== Warm cache: same key still warm, 200 concurrent requests ===");
await RunBurstAsync("warm-cache burst", concurrency: 200, noCache: false);
