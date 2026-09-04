using Inventory.Application;
using Inventory.Domain;
using Ordering.Application;
using Ordering.Domain;
using SharedKernel;
using Shipping.Application;
using Shipping.Domain;

namespace OrderFulfillment.Api;

/// The one place in the whole solution that knows every module's event
/// contracts and routes between them — modules themselves never see each
/// other's Application/Infrastructure layers, only this composition root
/// does. In production, this dispatch table is what each module's own
/// outbox relay calls when it delivers a message from Service Bus (the
/// exact mechanics already built and proven in ServiceBus-Demo and
/// OutboxPattern-Demo) — an in-process call here instead of a network hop,
/// but the routing logic itself doesn't change either way.
public class InProcessEventDispatcher(IServiceProvider services) : IIntegrationEventPublisher
{
    public async Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        switch (@event)
        {
            case OrderPlaced orderPlaced:
                await sp.GetRequiredService<ReserveStockOnOrderPlacedHandler>().HandleAsync(orderPlaced, cancellationToken);
                break;

            case StockReserved stockReserved:
                await sp.GetRequiredService<ConfirmOrderOnStockReservedHandler>().HandleAsync(stockReserved, cancellationToken);
                break;

            case StockReservationFailed failed:
                await sp.GetRequiredService<CancelOrderOnStockReservationFailedHandler>().HandleAsync(failed, cancellationToken);
                break;

            case OrderConfirmed orderConfirmed:
                await sp.GetRequiredService<CreateShipmentOnOrderConfirmedHandler>().HandleAsync(orderConfirmed, cancellationToken);
                await sp.GetRequiredService<Notifications.Application.NotificationHandlers>().HandleAsync(orderConfirmed, cancellationToken);
                break;

            case OrderCancelled orderCancelled:
                await sp.GetRequiredService<Notifications.Application.NotificationHandlers>().HandleAsync(orderCancelled, cancellationToken);
                break;

            case ShipmentCreated shipmentCreated:
                await sp.GetRequiredService<Notifications.Application.NotificationHandlers>().HandleAsync(shipmentCreated, cancellationToken);
                break;

            case OrderFulfilled:
                break; // nothing subscribes to this yet in the scaffold

            default:
                throw new InvalidOperationException($"No handler registered for event type {@event.GetType().Name}.");
        }
    }
}
