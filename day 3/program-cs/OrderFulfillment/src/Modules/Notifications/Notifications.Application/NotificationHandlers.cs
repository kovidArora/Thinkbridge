using Microsoft.Extensions.Logging;
using Ordering.Domain;
using Shipping.Domain;

namespace Notifications.Application;

/// No aggregate, no persistence — this module has no state of its own,
/// it only reacts. Scaffold only: a real implementation sends an actual
/// email/SMS here instead of logging.
public class NotificationHandlers(ILogger<NotificationHandlers> logger)
{
    public Task HandleAsync(OrderConfirmed orderConfirmed, CancellationToken cancellationToken)
    {
        logger.LogInformation("Would email customer: order {OrderId} confirmed.", orderConfirmed.OrderId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(OrderCancelled orderCancelled, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Would email customer: order {OrderId} cancelled ({Reason}).", orderCancelled.OrderId, orderCancelled.Reason);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ShipmentCreated shipmentCreated, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Would email customer: order {OrderId} shipped (shipment {ShipmentId}).",
            shipmentCreated.OrderId, shipmentCreated.ShipmentId);
        return Task.CompletedTask;
    }
}
