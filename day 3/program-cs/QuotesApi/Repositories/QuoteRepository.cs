using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;
namespace QuotesApi.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly QuotesDbContext _db;
    private readonly ILogger<QuoteRepository> _logger;
    private readonly AuthorStatsQueryMetrics _authorStatsMetrics;

    public QuoteRepository(
        QuotesDbContext db,
        ILogger<QuoteRepository> logger,
        AuthorStatsQueryMetrics authorStatsMetrics)
    {
    _db = db;
    _logger = logger;
    _authorStatsMetrics = authorStatsMetrics;
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
public async Task<List<AuthorStatsDto>> GetAuthorStatsAsync(CancellationToken cancellationToken)
{
    _authorStatsMetrics.RecordDbQuery();

    return await _db.Quotes
        .AsNoTracking()
        .Where(q => !q.IsDeleted)
        .GroupBy(q => q.Author)
        .Select(g => new AuthorStatsDto(g.Key, g.Count()))
        .ToListAsync(cancellationToken);
}

}