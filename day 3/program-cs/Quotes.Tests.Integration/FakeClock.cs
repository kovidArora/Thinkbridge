using QuotesApi.Repositories;

namespace Quotes.Tests.Integration;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
