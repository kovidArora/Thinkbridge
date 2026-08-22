using System.Diagnostics;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

const string SourceDb = @"..\QuotesApi\quotes.db";
const string DbFile = "quotes-benchmark.db";
const int Iterations = 200;
const int WarmupIterations = 10;

File.Copy(SourceDb, DbFile, overwrite: true);

var connectionString = $"Data Source={DbFile}";

// --- Print the exact SQL each approach runs ---
await using (var ctx = NewContext())
{
    var efQuery = ctx.Quotes
        .AsNoTracking()
        .Where(q => !q.IsDeleted)
        .GroupBy(q => q.Author)
        .Select(g => new { Author = g.Key, QuoteCount = g.Count() });

    Console.WriteLine("=== EF Core generated SQL ===");
    Console.WriteLine(efQuery.ToQueryString());
}

const string dapperSql = """
    SELECT Author, COUNT(*) AS QuoteCount
    FROM Quotes
    WHERE IsDeleted = 0
    GROUP BY Author
    """;

Console.WriteLine("\n=== Dapper hand-written SQL ===");
Console.WriteLine(dapperSql);

// --- Warm up both paths (JIT, connection pool, page cache) ---
for (var i = 0; i < WarmupIterations; i++)
{
    await RunEf();
    await RunDapper();
}

// --- Time EF ---
var efTimes = new List<double>();
int efCount = 0;
for (var i = 0; i < Iterations; i++)
{
    var sw = Stopwatch.StartNew();
    efCount = await RunEf();
    sw.Stop();
    efTimes.Add(sw.Elapsed.TotalMilliseconds);
}

// --- Time Dapper ---
var dapperTimes = new List<double>();
int dapperCount = 0;
for (var i = 0; i < Iterations; i++)
{
    var sw = Stopwatch.StartNew();
    dapperCount = await RunDapper();
    sw.Stop();
    dapperTimes.Add(sw.Elapsed.TotalMilliseconds);
}

Console.WriteLine($"\n=== Results over {Iterations} iterations (rows returned: EF={efCount}, Dapper={dapperCount}) ===");
Console.WriteLine($"EF Core : avg {efTimes.Average():F3} ms | min {efTimes.Min():F3} ms | p95 {Percentile(efTimes, 95):F3} ms");
Console.WriteLine($"Dapper  : avg {dapperTimes.Average():F3} ms | min {dapperTimes.Min():F3} ms | p95 {Percentile(dapperTimes, 95):F3} ms");
Console.WriteLine($"Dapper is {efTimes.Average() / dapperTimes.Average():F2}x the speed of EF on average.");

SqliteConnection.ClearAllPools();
File.Delete(DbFile);

async Task<int> RunEf()
{
    await using var ctx = NewContext();
    var result = await ctx.Quotes
        .AsNoTracking()
        .Where(q => !q.IsDeleted)
        .GroupBy(q => q.Author)
        .Select(g => new { Author = g.Key, QuoteCount = g.Count() })
        .ToListAsync();
    return result.Count;
}

async Task<int> RunDapper()
{
    await using var connection = new SqliteConnection(connectionString);
    var result = await connection.QueryAsync(dapperSql);
    return result.AsList().Count;
}

double Percentile(List<double> values, int p)
{
    var sorted = values.OrderBy(v => v).ToList();
    var index = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
}

BenchmarkDbContext NewContext() => new(connectionString);

class BenchmarkDbContext : DbContext
{
    private readonly string _connectionString;

    public BenchmarkDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DbSet<Quote> Quotes => Set<Quote>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSqlite(_connectionString);
}

class Quote
{
    public int Id { get; set; }
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsDeleted { get; set; }
}
