namespace QuotesApi;

/// Counts how many times the actual GROUP BY query behind /api/authors/stats
/// ran against the database — exists purely to make the cache's effect (and
/// stampede protection specifically) measurable from outside the process,
/// rather than trusted on faith.
public sealed class AuthorStatsQueryMetrics
{
    private long _dbQueryCount;

    public void RecordDbQuery() => Interlocked.Increment(ref _dbQueryCount);

    public long DbQueryCount => Interlocked.Read(ref _dbQueryCount);
}
