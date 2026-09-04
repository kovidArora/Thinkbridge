using Inventory.Domain;

namespace Ordering.Application;

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
