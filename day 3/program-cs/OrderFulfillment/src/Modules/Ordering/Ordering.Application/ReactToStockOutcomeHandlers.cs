using Inventory.Domain;

namespace Ordering.Application;

// runs when Inventory says StockReserved: load the order, confirm it, save
// (saving here is what queues the OrderConfirmed event for the next hop)
public class ConfirmOrderOnStockReservedHandler(IOrderRepository repository, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(StockReserved stockReserved, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(stockReserved.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {stockReserved.OrderId} not found.");

        order.Confirm();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// runs when Inventory says StockReservationFailed: load the order, cancel it, save
public class CancelOrderOnStockReservationFailedHandler(IOrderRepository repository, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(StockReservationFailed failure, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(failure.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {failure.OrderId} not found.");

        order.Cancel(failure.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
