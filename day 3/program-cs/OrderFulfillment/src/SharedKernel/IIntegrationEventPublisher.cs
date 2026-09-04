namespace SharedKernel;

/// For the (rarer) case where a reaction handler needs to publish an event
/// that isn't naturally "raised by an aggregate as part of its own state
/// change" — e.g. Inventory replying StockReserved(OrderId), which is about
/// Ordering's aggregate, not Inventory's. Still lands in the same module's
/// outbox table underneath; this is just how Application reaches it without
/// depending on Infrastructure directly.
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken);
}
