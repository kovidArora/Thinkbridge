using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

const string DbFile = "changetracker-demo.db";
const int RowCount = 10_000;

if (File.Exists(DbFile)) File.Delete(DbFile);

await using (var ctx = NewContext())
{
    await ctx.Database.EnsureCreatedAsync();

    var authors = Enumerable.Range(1, 50)
        .Select(i => new Author { Name = $"Author {i}" })
        .ToList();
    ctx.Authors.AddRange(authors);
    await ctx.SaveChangesAsync();

    var rnd = new Random(42);
    var quotes = Enumerable.Range(1, RowCount)
        .Select(i => new Quote
        {
            Text = $"Quote number {i}",
            AuthorId = authors[rnd.Next(authors.Count)].Id
        })
        .ToList();
    ctx.Quotes.AddRange(quotes);
    await ctx.SaveChangesAsync();
}

Console.WriteLine($"Seeded {RowCount} quotes across 50 authors.\n");

Console.WriteLine("=== Demo 1: Identity resolution ===");
await using (var ctx = NewContext())
{
    var authorDirect = await ctx.Authors.FirstAsync(a => a.Id == 7);

    var quoteWithAuthor = await ctx.Quotes
        .Include(q => q.Author)
        .FirstAsync(q => q.AuthorId == 7);

    var authorViaAnotherQuote = await ctx.Quotes
        .Include(q => q.Author)
        .Where(q => q.AuthorId == 7)
        .Select(q => q.Author!)
        .Skip(1)
        .FirstAsync();

    Console.WriteLine($"authorDirect         : {RuntimeHelpers(authorDirect)}");
    Console.WriteLine($"quoteWithAuthor.Author: {RuntimeHelpers(quoteWithAuthor.Author!)}");
    Console.WriteLine($"authorViaAnotherQuote : {RuntimeHelpers(authorViaAnotherQuote)}");
    Console.WriteLine($"ReferenceEquals(authorDirect, quoteWithAuthor.Author)    = {ReferenceEquals(authorDirect, quoteWithAuthor.Author)}");
    Console.WriteLine($"ReferenceEquals(authorDirect, authorViaAnotherQuote)     = {ReferenceEquals(authorDirect, authorViaAnotherQuote)}");
    Console.WriteLine($"Tracked entries in this context: {ctx.ChangeTracker.Entries().Count()} — the same Author(Id=7) is reused across all three queries instead of one instance per query, thanks to identity resolution\n");
}

Console.WriteLine("=== Demo 2: Tracked vs AsNoTracking on save ===");
await using (var ctx = NewContext())
{
    var tracked = await ctx.Quotes.FirstAsync(q => q.Id == 1);
    tracked.Text = "EDITED (tracked)";
    var changed = await ctx.SaveChangesAsync();
    Console.WriteLine($"Tracked entity: SaveChangesAsync() persisted {changed} row without an explicit Update() call.");
}

await using (var ctx = NewContext())
{
    var untracked = await ctx.Quotes.AsNoTracking().FirstAsync(q => q.Id == 2);
    untracked.Text = "EDITED (no-tracking)";
    var changed = await ctx.SaveChangesAsync();
    Console.WriteLine($"AsNoTracking entity: SaveChangesAsync() persisted {changed} rows (mutation is invisible to the change tracker; needs ctx.Update(untracked) to take effect).\n");
}

Console.WriteLine("=== Demo 3: 10k-row read benchmark ===");
var trackedResult = await Measure("Tracked", async () =>
{
    await using var ctx = NewContext();
    return await ctx.Quotes.ToListAsync();
});

var noTrackingResult = await Measure("AsNoTracking", async () =>
{
    await using var ctx = NewContext();
    return await ctx.Quotes.AsNoTracking().ToListAsync();
});

Console.WriteLine();
Console.WriteLine($"{"Variant",-14}{"Rows",-8}{"Elapsed (ms)",-15}{"Allocated (KB)",-16}");
Console.WriteLine($"{"Tracked",-14}{trackedResult.Count,-8}{trackedResult.Elapsed.TotalMilliseconds,-15:F2}{trackedResult.AllocatedBytes / 1024.0,-16:F1}");
Console.WriteLine($"{"AsNoTracking",-14}{noTrackingResult.Count,-8}{noTrackingResult.Elapsed.TotalMilliseconds,-15:F2}{noTrackingResult.AllocatedBytes / 1024.0,-16:F1}");
Console.WriteLine();
Console.WriteLine($"Time ratio (tracked / no-tracking):   {trackedResult.Elapsed.TotalMilliseconds / noTrackingResult.Elapsed.TotalMilliseconds:F2}x");
Console.WriteLine($"Alloc ratio (tracked / no-tracking):  {(double)trackedResult.AllocatedBytes / noTrackingResult.AllocatedBytes:F2}x");

Console.WriteLine("=== Demo 4: Generated SQL — full entity vs projected DTO ===");
var fullEntitySql = new List<string>();
await using (var ctx = NewContext(sql => fullEntitySql.Add(sql)))
{
    var quotes = await ctx.Quotes.Where(q => q.AuthorId == 7).ToListAsync();
    Console.WriteLine($"Full-entity query returned {quotes.Count} rows.");
}
Console.WriteLine("SQL generated (full entity):");
Console.WriteLine(LastCommandText(fullEntitySql));

var projectedSql = new List<string>();
await using (var ctx = NewContext(sql => projectedSql.Add(sql)))
{
    var dtos = await ctx.Quotes
        .Where(q => q.AuthorId == 7)
        .Select(q => new QuoteSummaryDto(q.Id, q.Text))
        .ToListAsync();
    Console.WriteLine($"Projected DTO query returned {dtos.Count} rows.");
}
Console.WriteLine("SQL generated (projected DTO — no AuthorId column fetched):");
Console.WriteLine(LastCommandText(projectedSql));
Console.WriteLine();

Console.WriteLine("=== Demo 5: Catching accidental client-side evaluation ===");
var buggySql = new List<string>();
await using (var ctx = NewContext(sql => buggySql.Add(sql)))
{
    var buggyResult = ctx.Quotes.ToList().Where(q => q.AuthorId == 7).ToList();
    Console.WriteLine($"Buggy version: {buggyResult.Count} rows in the final result, but the SQL log shows no WHERE clause:");
    Console.WriteLine(LastCommandText(buggySql));
}

var fixedSql = new List<string>();
await using (var ctx = NewContext(sql => fixedSql.Add(sql)))
{
    var fixedResult = await ctx.Quotes.Where(q => q.AuthorId == 7).ToListAsync();
    Console.WriteLine($"Fixed version: {fixedResult.Count} rows, and the SQL log now shows the WHERE clause doing the filtering:");
    Console.WriteLine(LastCommandText(fixedSql));
}

static string LastCommandText(List<string> log) =>
    log.LastOrDefault(l => l.Contains("Executed DbCommand")) is { } entry
        ? entry[(entry.IndexOf("SELECT", StringComparison.Ordinal) is var i && i >= 0 ? i : 0)..]
        : "(no command logged)";

static string RuntimeHelpers(Author a) => $"Id={a.Id,-3} hash=0x{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a):x8}";

static DemoContext NewContext(Action<string>? sqlSink = null)
{
    var builder = new DbContextOptionsBuilder<DemoContext>().UseSqlite($"Data Source={DbFile}");
    if (sqlSink is not null)
    {
        builder.LogTo(sqlSink, LogLevel.Information).EnableSensitiveDataLogging();
    }
    return new DemoContext(builder.Options);
}

static async Task<(int Count, TimeSpan Elapsed, long AllocatedBytes)> Measure(string label, Func<Task<List<Quote>>> action)
{
    (await action()).Clear();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var allocBefore = GC.GetAllocatedBytesForCurrentThread();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var rows = await action();
    sw.Stop();
    var allocAfter = GC.GetAllocatedBytesForCurrentThread();

    Console.WriteLine($"{label}: read {rows.Count} rows in {sw.Elapsed.TotalMilliseconds:F2} ms, allocated {(allocAfter - allocBefore) / 1024.0:F1} KB");
    return (rows.Count, sw.Elapsed, allocAfter - allocBefore);
}

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Quote> Quotes { get; set; } = new();
}

public class Quote
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public int AuthorId { get; set; }
    public Author? Author { get; set; }
}

public record QuoteSummaryDto(int Id, string Text);

public class DemoContext : DbContext
{
    public DemoContext(DbContextOptions<DemoContext> options) : base(options) { }

    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Quote> Quotes => Set<Quote>();
}
