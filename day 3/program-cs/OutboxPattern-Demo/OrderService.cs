namespace OutboxPatternDemo;

public class OrderService(AppDbContext db)
{
    public async Task<Guid> PlaceOrderAsync(string customerName, decimal amount)
    {
        var order = Order.Place(customerName, amount);
        var outboxMessage = OutboxMessage.For("OrderPlaced", new { order.Id, order.CustomerName, order.Amount });

        db.Orders.Add(order);
        db.OutboxMessages.Add(outboxMessage);

        // One SaveChangesAsync = one transaction: the order row and its
        // outbox row either both land or neither does. There is no window
        // where the domain write committed but the "publish this" record
        // didn't, even if the process is killed the very next instruction.
        await db.SaveChangesAsync();

        return order.Id;
    }
}
