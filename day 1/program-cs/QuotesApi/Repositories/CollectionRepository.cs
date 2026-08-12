using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;
using QuotesApi.Data;
namespace QuotesApi.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly QuotesDbContext _context;

    public CollectionRepository(QuotesDbContext context)
    {
        _context = context;
    }

    public async Task<Collection?> GetById(
    int id,
    CancellationToken cancellationToken)
{
    return await _context.Collections
        .Include(c => c.Items)
        .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}

public async Task Add(
    Collection collection,
    CancellationToken cancellationToken)
{
    await _context.Collections.AddAsync(collection, cancellationToken);
    await _context.SaveChangesAsync(cancellationToken);
}

public async Task Update(
    Collection collection,
    CancellationToken cancellationToken)
{
    _context.Collections.Update(collection);
    await _context.SaveChangesAsync(cancellationToken);
}

public async Task Delete(
    Collection collection,
    CancellationToken cancellationToken)
{
    _context.Collections.Remove(collection);
    await _context.SaveChangesAsync(cancellationToken);
}
}