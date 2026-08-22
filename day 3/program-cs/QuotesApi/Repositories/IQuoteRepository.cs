using QuotesApi.Models;

namespace QuotesApi.Repositories;

public record QuoteWithAuthorDto(int Id, string Author, string Text, string AuthorEmail);

public record AuthorStatsDto(string Author, int QuoteCount);

public interface IQuoteRepository
{
    Task<List<Quote>> GetQuotesAsync(
        int page,
        int size,
        CancellationToken cancellationToken);

    Task<List<AuthorStatsDto>> GetAuthorStatsAsync(
        CancellationToken cancellationToken);

    Task<Quote?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken);
}