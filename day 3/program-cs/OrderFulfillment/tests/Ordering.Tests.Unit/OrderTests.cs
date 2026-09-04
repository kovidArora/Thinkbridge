using Ordering.Domain;
using Xunit;

namespace Ordering.Tests.Unit;

public class OrderTests
{
    [Fact]
    public void Place_NoLines_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Order.Place(Guid.NewGuid(), []));
    }

    [Fact]
    public void Place_ValidLines_RaisesOrderPlacedWithCorrectTotal()
    {
        var order = Order.Place(Guid.NewGuid(), [OrderLine.Create("SKU-1", 2, 10m), OrderLine.Create("SKU-2", 1, 5m)]);

        Assert.Equal(25m, order.Total);
        Assert.Equal(OrderStatus.Placed, order.Status);
        var placed = Assert.IsType<OrderPlaced>(Assert.Single(order.PendingEvents));
        Assert.Equal(25m, placed.Total);
        Assert.Equal(2, placed.Lines.Count);
    }

    [Fact]
    public void Confirm_FromPlaced_Succeeds()
    {
        var order = Order.Place(Guid.NewGuid(), [OrderLine.Create("SKU-1", 1, 10m)]);
        order.ClearPendingEvents();

        order.Confirm();

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.IsType<OrderConfirmed>(Assert.Single(order.PendingEvents));
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_Throws()
    {
        var order = Order.Place(Guid.NewGuid(), [OrderLine.Create("SKU-1", 1, 10m)]);
        order.Confirm();

        Assert.Throws<InvalidOperationException>(() => order.Confirm());
    }

    [Fact]
    public void Cancel_AfterFulfilled_Throws()
    {
        var order = Order.Place(Guid.NewGuid(), [OrderLine.Create("SKU-1", 1, 10m)]);
        order.Confirm();
        order.MarkFulfilled();

        Assert.Throws<InvalidOperationException>(() => order.Cancel("too late"));
    }

    [Fact]
    public void MarkFulfilled_BeforeConfirmed_Throws()
    {
        var order = Order.Place(Guid.NewGuid(), [OrderLine.Create("SKU-1", 1, 10m)]);

        Assert.Throws<InvalidOperationException>(() => order.MarkFulfilled());
    }
}
