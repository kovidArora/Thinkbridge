namespace Ordering.Domain;

public class OrderLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ProductSku { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;

    private OrderLine() { }

    public static OrderLine Create(string productSku, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productSku))
        {
            throw new ArgumentException("Product SKU is required.", nameof(productSku));
        }
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        return new OrderLine { ProductSku = productSku, Quantity = quantity, UnitPrice = unitPrice };
    }
}
