using SharedKernel;

namespace Inventory.Domain;

/// Inventory's own aggregate — Ordering never touches this directly, and
/// vice versa. The only thing that crosses the boundary is the events below.
public class StockItem : AggregateRoot
{
    public string ProductSku { get; private set; } = string.Empty;
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    private StockItem() { }

    public static StockItem Stock(string productSku, int quantityOnHand) => new()
    {
        ProductSku = productSku,
        QuantityOnHand = quantityOnHand,
    };

    public bool TryReserve(int quantity)
    {
        if (quantity > QuantityAvailable)
        {
            return false;
        }

        QuantityReserved += quantity;
        return true;
    }
}
