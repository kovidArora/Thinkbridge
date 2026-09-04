using SharedKernel;

namespace Ordering.Domain;

public record OrderPlacedLine(string Sku, int Quantity);

// ValueTuples don't round-trip through System.Text.Json (they serialize as
// {Item1, Item2}, losing the field names, and deserializing back into a
// tuple silently produces nulls) — a plain record is what actually survives
// the outbox's serialize-then-deserialize trip.
public record OrderPlaced(Guid OrderId, Guid CustomerId, decimal Total, IReadOnlyList<OrderPlacedLine> Lines) : IntegrationEvent;

public record OrderConfirmed(Guid OrderId) : IntegrationEvent;

public record OrderCancelled(Guid OrderId, string Reason) : IntegrationEvent;

public record OrderFulfilled(Guid OrderId) : IntegrationEvent;
