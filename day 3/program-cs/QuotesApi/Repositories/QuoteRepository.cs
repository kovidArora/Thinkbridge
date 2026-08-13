using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;
namespace QuotesApi.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly QuotesDbContext _db;
    private readonly ILogger<QuoteRepository> _logger;

    private readonly IClock _clock;
    public QuoteRepository(
        QuotesDbContext db,
        IClock clock,
        ILogger<QuoteRepository> logger)
    {
    _db = db;
    _logger = logger;
    _clock = clock;
    }

public async Task<List<Quote>> GetQuotesAsync(
    int page,
    int size,
    CancellationToken cancellationToken)
{
    return await _db.Quotes
        .AsNoTracking()
        .Where(q => !q.IsDeleted)
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
        .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, cancellationToken);
}

public async Task<bool> DeleteAsync(
    int id,
    CancellationToken cancellationToken)
{
    var quote = await _db.Quotes
        .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, cancellationToken);

    if (quote is null)
    {
        _logger.LogWarning(
            "Quote {QuoteId} was not found for deletion",
            id);

        return false;
    }

    quote.Delete();
    await _db.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(
        "Deleted quote {QuoteId}",
        id);

    return true;
}
public async Task<Quote> AddAsync( Quote quote, CancellationToken cancellationToken) {
    quote.PublishedAt = _clock.UtcNow;
    
    _db.Quotes.Add(quote);
    await _db.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(
        "Created quote {QuoteId} for user {UserId}",
        quote.Id,
        quote.CreatedByUserId);

    return quote;
}


}