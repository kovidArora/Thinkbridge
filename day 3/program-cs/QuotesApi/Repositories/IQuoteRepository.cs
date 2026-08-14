using QuotesApi.Models;

namespace QuotesApi.Repositories;

public record QuoteWithAuthorDto(int Id, string Author, string Text, string AuthorEmail);

public interface IQuoteRepository
{
    Task<List<Quote>> GetQuotesAsync(
        int page,
        int size,
        CancellationToken cancellationToken);

    Task<List<QuoteWithAuthorDto>> GetQuotesWithAuthorEmailAsync(
        int page,
        int size,
        CancellationToken cancellationToken);

    Task<Quote?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<Quote> AddAsync(
        Quote quote,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken);
}