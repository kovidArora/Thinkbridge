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

    public async Task<Collection?> GetById(int id)
    {
        return await _context.Collections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task Add(Collection collection)
    {
        await _context.Collections.AddAsync(collection);
        await _context.SaveChangesAsync();
    }

    public async Task Update(Collection collection)
    {
        _context.Collections.Update(collection);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(Collection collection)
    {
        _context.Collections.Remove(collection);
        await _context.SaveChangesAsync();
    }
}