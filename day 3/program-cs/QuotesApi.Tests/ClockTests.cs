using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;
using Xunit;

namespace QuotesApi.Tests;

public class ClockTests
{
    [Fact]
    public async Task AddAsync_UsesFakeClock()
    {
        var fixedTime = new DateTimeOffset(
            2026, 8, 11, 10, 0, 0, TimeSpan.Zero);

        var clock = new FakeClock(fixedTime);

        var options = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new QuotesDbContext(options);

        using var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<QuoteRepository>();

        var repository = new QuoteRepository(db, clock, logger);

        var (quote, error) = Quote.Create(
            "Albert Einstein",
            "Life is like riding a bicycle.");

        Assert.NotNull(quote);

        var result = await repository.AddAsync(
            quote!,
            CancellationToken.None);

        Assert.Equal(fixedTime, result.PublishedAt);
    }

    private class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; }

        public FakeClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }
    }
}