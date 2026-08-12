using Microsoft.EntityFrameworkCore;
using OrderSystem.Application.Interfaces;
using OrderSystem.Domain.Entities;

namespace OrderSystem.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context) => _context = context;

    public async Task<Dictionary<int, Product>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var idList = ids.ToList();
        var products = await _context.Products
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);

        return products.ToDictionary(p => p.Id);
    }
}
