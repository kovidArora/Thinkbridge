using Inventory.Domain;

namespace Inventory.Application;

public interface IStockItemRepository
{
    Task<StockItem?> GetBySkuAsync(string productSku, CancellationToken cancellationToken);
}
