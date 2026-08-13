namespace QuotesApi.Repositories;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}