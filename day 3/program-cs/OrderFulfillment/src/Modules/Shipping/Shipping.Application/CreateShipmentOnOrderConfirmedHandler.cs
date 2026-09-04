using Ordering.Domain;
using SharedKernel;
using Shipping.Domain;

namespace Shipping.Application;

/// Scaffold only — shows the shape of the reaction (and that Shipping only
/// ever learns about an order through the OrderConfirmed event, never by
/// reaching into Ordering's own tables), but has no repository/persistence
/// wired up yet. Real persistence would follow the exact same pattern as
/// Ordering.Infrastructure.
public class CreateShipmentOnOrderConfirmedHandler(IIntegrationEventPublisher publisher)
{
    public async Task HandleAsync(OrderConfirmed orderConfirmed, CancellationToken cancellationToken)
    {
        var shipment = Shipment.CreateFor(orderConfirmed.OrderId);

        foreach (var @event in shipment.PendingEvents)
        {
            await publisher.PublishAsync(@event, cancellationToken);
        }
    }
}
