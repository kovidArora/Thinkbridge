namespace OutboxPatternDemo;

public class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string CustomerName { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Order() { }

    public static Order Place(string customerName, decimal amount) => new()
    {
        CustomerName = customerName,
        Amount = amount,
    };
}
