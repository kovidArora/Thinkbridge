using SharedKernel;

namespace Shipping.Domain;

public class Shipment : AggregateRoot
{
    public Guid OrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Shipment() { }

    public static Shipment CreateFor(Guid orderId)
    {
        var shipment = new Shipment { OrderId = orderId };
        shipment.Raise(new ShipmentCreated(orderId, shipment.Id));
        return shipment;
    }
}

public record ShipmentCreated(Guid OrderId, Guid ShipmentId) : IntegrationEvent;
