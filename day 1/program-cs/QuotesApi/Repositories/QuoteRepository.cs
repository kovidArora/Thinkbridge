using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly QuotesDbContext _db;
    private readonly ILogger<QuoteRepository> _logger;

    public QuoteRepository(
        QuotesDbContext db,
        ILogger<QuoteRepository> logger)
    {
    _db = db;
    _logger = logger;
    }

    public async Task<List<Quote>> GetQuotesAsync(
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        return await _db.Quotes
            .AsNoTracking()
            .OrderBy(q => q.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
    }

    public async Task<Quote?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _db.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

public async Task<Quote> AddAsync(
    Quote quote,
    CancellationToken cancellationToken)
{
    _db.Quotes.Add(quote);
    await _db.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(
        "Created quote {QuoteId} by {Author}",
        quote.Id,
        quote.Author);

    return quote;
}

    public async Task<bool> DeleteAsync(
    int id,
    CancellationToken cancellationToken)
{
    var quote = await _db.Quotes
        .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    if (quote is null)
    {
        _logger.LogWarning(
            "Quote {QuoteId} was not found for deletion",
            id);

        return false;
    }

    _db.Quotes.Remove(quote);
    await _db.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(
        "Deleted quote {QuoteId}",
        id);

    return true;
}
}