using SharedKernel;

namespace Inventory.Domain;

/// Inventory's reply to Ordering's OrderPlaced — not a direct response,
/// just another event published independently, on Inventory's own timeline.
public record StockReserved(Guid OrderId) : IntegrationEvent;

public record StockReservationFailed(Guid OrderId, string Reason) : IntegrationEvent;
