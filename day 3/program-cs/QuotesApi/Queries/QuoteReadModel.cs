using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Repositories;

namespace QuotesApi.Queries;

public class QuoteReadModel
{
    private readonly QuotesDbContext _db;

    public QuoteReadModel(QuotesDbContext db)
    {
        _db = db;
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
}
