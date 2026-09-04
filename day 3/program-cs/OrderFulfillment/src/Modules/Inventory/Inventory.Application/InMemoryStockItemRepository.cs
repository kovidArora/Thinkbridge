using System.Collections.Concurrent;
using Inventory.Domain;

namespace Inventory.Application;

/// Scaffold-only stand-in for a real EF-backed repository (see
/// Ordering.Infrastructure.EfOrderRepository for the real pattern this would
/// follow) — kept in-memory so this module doesn't need its own database
/// wired up just to demonstrate the async flow shape.
public class InMemoryStockItemRepository : IStockItemRepository
{
    private readonly ConcurrentDictionary<string, StockItem> _items = new();

    public void Seed(string sku, int quantityOnHand) => _items[sku] = StockItem.Stock(sku, quantityOnHand);

    public Task<StockItem?> GetBySkuAsync(string productSku, CancellationToken cancellationToken) =>
        Task.FromResult(_items.GetValueOrDefault(productSku));
}
