using SharedKernel;

namespace Ordering.Domain;

/// The core aggregate. Everything about an order's lifecycle — what lines it
/// can have, what transitions are legal — is enforced here, not in a service
/// or a controller. Nothing outside this class ever sets Status directly.
public class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = [];

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    public decimal Total => _lines.Sum(l => l.LineTotal);

    private Order() { }

    public static Order Place(Guid customerId, IEnumerable<OrderLine> lines)
    {
        var lineList = lines.ToList();
        if (lineList.Count == 0)
        {
            throw new InvalidOperationException("An order must have at least one line.");
        }

        var order = new Order { CustomerId = customerId, Status = OrderStatus.Placed };
        order._lines.AddRange(lineList);

        order.Raise(new OrderPlaced(
            order.Id,
            order.CustomerId,
            order.Total,
            lineList.Select(l => new OrderPlacedLine(l.ProductSku, l.Quantity)).ToList()));

        return order;
    }

    /// Called when Inventory's async reply says stock was reserved.
    public void Confirm()
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOperationException($"Cannot confirm an order in status {Status}.");
        }

        Status = OrderStatus.Confirmed;
        Raise(new OrderConfirmed(Id));
    }

    /// Called when Inventory's async reply says stock could NOT be reserved,
    /// or a customer/operator cancels before fulfillment.
    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Fulfilled or OrderStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel an order in status {Status}.");
        }

        Status = OrderStatus.Cancelled;
        Raise(new OrderCancelled(Id, reason));
    }

    /// Called when Shipping's async reply says the shipment was created.
    public void MarkFulfilled()
    {
        if (Status != OrderStatus.Confirmed)
        {
            throw new InvalidOperationException($"Cannot fulfill an order in status {Status}.");
        }

        Status = OrderStatus.Fulfilled;
        Raise(new OrderFulfilled(Id));
    }
}
