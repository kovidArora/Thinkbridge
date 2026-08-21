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
public async Task<List<QuoteWithAuthorDto>> GetQuotesWithAuthorEmailAsync(
    int page,
    int size,
    CancellationToken cancellationToken)
{
    var quotes = await _db.Quotes
        .AsNoTracking()
        .Where(q => !q.IsDeleted)
        .OrderBy(q => q.Id)
        .Skip((page - 1) * size)
        .Take(size)
        .ToListAsync(cancellationToken);

    var userIds = quotes.Select(q => q.CreatedByUserId).Distinct().ToList();

    var emailsByUserId = await _db.Users
        .AsNoTracking()
        .Where(u => userIds.Contains(u.Id))
        .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

    return quotes
        .Select(quote => new QuoteWithAuthorDto(
            quote.Id,
            quote.Author,
            quote.Text,
            emailsByUserId.GetValueOrDefault(quote.CreatedByUserId, "unknown")))
        .ToList();
}

public async Task<List<AuthorStatsDto>> GetAuthorStatsAsync(CancellationToken cancellationToken)
{
    var authors = await _db.Quotes
        .AsNoTracking()
        .Select(q => q.Author)
        .Distinct()
        .ToListAsync(cancellationToken);

    var stats = new List<AuthorStatsDto>();

    foreach (var author in authors)
    {
        var count = await _db.Quotes
            .AsNoTracking()
            .CountAsync(q => q.Author == author && !q.IsDeleted, cancellationToken);

        stats.Add(new AuthorStatsDto(author, count));
    }

    return stats;
}

public async Task<Quote> AddAsync( Quote quote, CancellationToken cancellationToken) {
    quote.PublishedAt = _clock.UtcNow;
    
    _db.Quotes.Add(quote);
    await _db.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(
        "Created quote {QuoteId} by {Author}",
        quote.Id,
        quote.Author);

    return quote;
}


}