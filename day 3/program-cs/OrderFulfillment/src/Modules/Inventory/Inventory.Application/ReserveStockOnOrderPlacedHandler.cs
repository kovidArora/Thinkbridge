using Inventory.Domain;
using Ordering.Domain;
using SharedKernel;

namespace Inventory.Application;

/// The consuming side of the OrderPlaced -> StockReserved/StockReservationFailed
/// async flow. Runs whenever this module's outbox relay delivers an
/// OrderPlaced event — see the one-page design for the full sequence.
public class ReserveStockOnOrderPlacedHandler(
    IStockItemRepository repository,
    IIntegrationEventPublisher publisher)
{
    // check every line's stock. sku missing or not enough left -> publish
    // StockReservationFailed and bail immediately. all lines ok -> publish
    // StockReserved once, after the loop
    public async Task HandleAsync(OrderPlaced orderPlaced, CancellationToken cancellationToken)
    {
        foreach (var line in orderPlaced.Lines)
        {
            var stockItem = await repository.GetBySkuAsync(line.Sku, cancellationToken);

            if (stockItem is null || !stockItem.TryReserve(line.Quantity))
            {
                await publisher.PublishAsync(
                    new StockReservationFailed(orderPlaced.OrderId, $"Insufficient stock for {line.Sku}."),
                    cancellationToken);
                return;
            }
        }

        await publisher.PublishAsync(new StockReserved(orderPlaced.OrderId), cancellationToken);
    }
}
