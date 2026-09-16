# OrderFulfillment

A scaffolded modular monolith demonstrating how independent business modules
(Ordering, Inventory, Shipping, Notifications) communicate through published
events and a transactional outbox — without a real message broker, and
without knowing about each other's internals.

## What it does

Places an order, checks stock, and confirms or cancels it automatically —
end to end, without the caller waiting on anything past the initial save.

1. `POST /orders` saves a new order and returns its id immediately.
2. In the background, a worker picks up the pending event, asks Inventory
   to reserve stock, and confirms or cancels the order based on the result.
3. `GET /orders/{id}` lets you check the order's current status at any point.

## Architecture

```
src/
  SharedKernel/              types every module can depend on
                             (IntegrationEvent, AggregateRoot, OutboxMessage,
                             IIntegrationEventPublisher)
  Modules/
    Ordering/
      Ordering.Domain/       Order, OrderLine, OrderStatus, events
      Ordering.Application/  command handlers, IOrderRepository
      Ordering.Infrastructure/  OrderingDbContext, EF repositories
    Inventory/
      Inventory.Domain/      StockItem, events
      Inventory.Application/ handlers, in-memory repository (scaffold only)
    Shipping/
      Shipping.Domain/       Shipment, events
      Shipping.Application/  handlers (no persistence yet)
    Notifications/
      Notifications.Application/  handlers (no state, just logs)
host/
  OrderFulfillment.Api/      Program.cs, the composition root, the outbox
                             worker, and the in-process event dispatcher
tests/
  Ordering.Tests.Unit/       unit tests for the Order aggregate
```

Dependencies only ever flow one way: **Domain → Application → Infrastructure**,
and no module references another module directly. The only file allowed to
know about every module at once is `InProcessEventDispatcher` in the host
project — everything else depends on the `IIntegrationEventPublisher`
interface instead.

### The outbox pattern

Placing an order and publishing the fact that it happened must never
diverge — if one succeeded but not the other, the system would be silently
broken. So instead of publishing live, `OrderingDbContext.SaveChangesAsync`
writes any pending domain event into the same `OutboxMessages` table, in the
same transaction as the actual business write. A background service
(`OutboxDispatcherBackgroundService`) polls that table every 200ms, and
routes anything unprocessed through `InProcessEventDispatcher`.

In a real deployment, the same outbox row would be picked up by a relay
process and published to a real broker (see `ServiceBus-Demo` /
`OutboxPattern-Demo` for that fuller version) instead of being dispatched
in-process — the durability guarantee is identical either way.

## Running it

```bash
cd host/OrderFulfillment.Api
dotnet run --urls http://localhost:5095
```

Two SKUs are seeded on startup: `MUG-001` (10 in stock) and `SHIRT-001`
(0 in stock, deliberately, to exercise the cancellation path).

### Place an order

```bash
curl -X POST http://localhost:5095/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","lines":[{"productSku":"MUG-001","quantity":1,"unitPrice":9.99}]}'
```

### Force the outbox to process immediately

The worker polls every 200ms on its own, but this skips the wait:

```bash
curl -X POST http://localhost:5095/debug/dispatch-outbox
```

### Check an order's status

```bash
curl http://localhost:5095/orders/{orderId}
```

`status` is the `OrderStatus` enum as an integer: `0` Placed, `1` Confirmed,
`2` Fulfilled, `3` Cancelled.

### PowerShell note

`curl` is aliased to `Invoke-WebRequest` in PowerShell and doesn't accept
the same flags — either call the real binary with `curl.exe`, or use
`Invoke-RestMethod` instead, which handles quoting more predictably:

```powershell
Invoke-RestMethod -Uri http://localhost:5095/orders -Method Post `
  -ContentType "application/json" `
  -Body '{"customerId":"11111111-1111-1111-1111-111111111111","lines":[{"productSku":"MUG-001","quantity":1,"unitPrice":9.99}]}'
```

## Resetting local state

The database is a single SQLite file, created on startup via
`EnsureCreatedAsync()` if it doesn't already exist — it is **not** recreated
on every run, so data accumulates across restarts unless you delete it
first:

```bash
rm host/OrderFulfillment.Api/orderfulfillment.db*
```

## Tracing

OpenTelemetry is wired unconditionally for ASP.NET Core, HttpClient, and EF
Core instrumentation, plus a manually-instrumented span around each outbox
dispatch (`OutboxDispatcherBackgroundService.ActivitySource`). In
`Development`, a console exporter prints every span as it completes, so a
local run can be visually confirmed without an Application Insights
connection. Setting `APPLICATIONINSIGHTS_CONNECTION_STRING` additionally
ships the same traces to Azure Monitor — both can be active at once.

The outbox row carries the originating request's `traceparent`
(`OutboxMessage.TraceParent`), so the worker's later, asynchronous span
resumes the same trace the original HTTP request started, rather than
starting an unrelated one.

## Tests

```bash
cd tests/Ordering.Tests.Unit
dotnet test
```

Covers the `Order` aggregate's state-transition rules (placing, confirming,
cancelling, fulfilling, and the invalid transitions between them). Nothing
else in the solution — the handlers, the dispatcher, and the outbox worker
— has automated test coverage yet.

## Known gaps

- No validation-error handling at the API boundary: an invalid line (e.g.
  negative quantity) throws inside the domain layer and currently
  surfaces as a raw `500` instead of a clean `400`.
- A message that fails every retry has no dead-letter equivalent — it
  retries forever on every poll instead of eventually being quarantined
  (per-message failures are now isolated and logged; see ADR-001).
- Inventory and Shipping have no real persistence — an in-memory repository
  and no repository at all, respectively. Both are explicitly scaffolded
  this way; `Ordering.Infrastructure/EfOrderRepository.cs` is the pattern a
  real implementation would follow.
