# Order Fulfillment — Modular Monolith Design

## Why this slice
A small order-fulfillment flow: place an order, reserve stock, confirm or
cancel, ship, notify. Small enough to build in one sitting, but it genuinely
needs more than one bounded context — which is exactly what makes it worth
designing as a modular monolith rather than a single CRUD service or a set of
microservices nobody needs yet.

## Bounded contexts (modules)

| Module | Owns | Has its own persistence? |
|---|---|---|
| **Ordering** (core) | The order lifecycle: lines, total, status transitions | Yes — real EF Core + outbox, fully implemented |
| **Inventory** | Stock levels, reservations | Scaffolded (in-memory repository, same interfaces a real EF implementation would use) |
| **Shipping** | Creating a shipment once an order is confirmed | Scaffolded (no persistence yet) |
| **Notifications** | Telling the customer what happened | No state at all — pure reactor |

Each module is its own set of projects (`Domain` / `Application` / and
`Infrastructure` where it has real persistence). A module's `Domain` never
references another module. The only two things allowed to cross a module
boundary are: (a) another module's `Domain` project, purely to read its
public event *contracts* (the integration event records), never its
internals; (b) the Host, which is the one place allowed to know every module
exists at once. No module ever calls another module's repository or
database directly — that's the actual meaning of "modular" here, not just a
folder convention.

## Core aggregate: `Order`

Lives in `Ordering.Domain`. Everything about what an order is allowed to do
is enforced on this one class — nothing outside it ever sets `Status`
directly:

- `Place(customerId, lines)` — the only way an `Order` comes into existence; throws if given zero lines. Raises `OrderPlaced`.
- `Confirm()` — only legal from `Placed`. Raises `OrderConfirmed`.
- `Cancel(reason)` — legal from `Placed` or `Confirmed`, not from `Fulfilled`/`Cancelled`. Raises `OrderCancelled`.
- `MarkFulfilled()` — only legal from `Confirmed`. Raises `OrderFulfilled`.
- `Total` is a computed property (`Lines.Sum(...)`), never a settable field — there's no way to construct an order whose total doesn't match its lines.

## Async flows

Ordering never calls Inventory, Shipping, or Notifications directly — it
publishes a fact and moves on. Each flow below is: aggregate raises event →
event is written to *that module's own* outbox row in the *same* transaction
as the state change → a relay delivers it → the consuming module reacts with
its own aggregate/logic → (often) raises its own event.

```
1. PlaceOrderCommand
     → Order.Place()                              [Ordering]
     → raises OrderPlaced
                │
                ▼
2. Inventory reacts to OrderPlaced
     → tries to reserve each line's stock          [Inventory]
     → raises StockReserved  OR  StockReservationFailed
                │                         │
                ▼                         ▼
3a. Ordering reacts to StockReserved    3b. Ordering reacts to StockReservationFailed
     → Order.Confirm()                      → Order.Cancel(reason)
     → raises OrderConfirmed                → raises OrderCancelled
                │                                       │
                ▼                                       ▼
4. Shipping reacts to OrderConfirmed       Notifications reacts to OrderCancelled
     → Shipment.CreateFor(orderId)              → "order cancelled" email
     → raises ShipmentCreated
                │
      ┌─────────┴─────────┐
      ▼                   ▼
Notifications reacts   Notifications reacts
to OrderConfirmed      to ShipmentCreated
 → "order confirmed"    → "order shipped"
   email                  email
```

**Reliability guarantee**: every one of these arrows is durable, not
fire-and-forget — this is the exact transactional-outbox pattern already
built and load-tested in `OutboxPattern-Demo` earlier this session (domain
write + outbox row in one transaction; a crash between publish and
mark-sent produces a duplicate, never a loss; consumers dedupe). In
production, step 2→3 and 3→4 would cross a real broker — the same shape
already proven working in `ServiceBus-Demo` (topic + subscriptions,
competing consumers, dead-letter for a poison message). **This scaffold
swaps that broker hop for an in-process dispatcher** (`InProcessEventDispatcher`
+ `OutboxDispatcherBackgroundService` in the Host) purely so the design is
runnable without extra infrastructure — the routing logic itself is
identical either way, and replacing the dispatcher with a real Service Bus
relay is a Host-only change; no module's code changes.

## What's fully real vs. scaffolded

- **Ordering**: fully real. EF Core (SQLite), its own outbox table, a repository, a command handler, and unit tests on the aggregate's invariants.
- **Inventory / Shipping**: real domain logic and event contracts, but an in-memory repository (Inventory) or no repository at all (Shipping) instead of their own database — the interfaces are exactly what a real `Infrastructure` project would implement, following `Ordering.Infrastructure`'s pattern.
- **Notifications**: fully real for what it is — it has no state, so "real" just means it actually logs the right message for the right event, which it does.

Verified live, not just by reading the code: an order for in-stock goods
reaches `Confirmed`, triggers a real `Shipment`, and produces two log lines
from `Notifications`; an order for an out-of-stock SKU reaches `Cancelled`
with the actual failure reason attached — both confirmed by actually running
the host and placing both orders.
