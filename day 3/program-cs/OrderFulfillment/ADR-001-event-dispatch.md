# ADR-001: In-process event dispatch instead of a real message broker

## Status
Accepted (documented at scaffold time, Sept 4; defended under interview
critique, Sept 16).

## Context
Ordering, Inventory, Shipping, and Notifications need to react to each
other's events (`OrderPlaced`, `StockReserved`, `OrderConfirmed`, ...)
without depending on each other's internals. The proven pattern for this —
already built and load-tested separately in `ServiceBus-Demo` — is a real
broker: a topic per event stream, one subscription per consumer, competing
consumers within a subscription, dead-lettering for messages that can never
succeed.

Wiring the real broker into this capstone means provisioning Azure Service
Bus. Topics require the Standard tier — there is no free tier for topics,
unlike the API and SQL pieces this project also uses. The project's Azure
subscription was a free trial with a fixed expiry, and did in fact expire
during the build (2026-09-14/15) — so any design that made real Service Bus
a hard runtime dependency would have stopped working entirely, independent
of the code's own correctness.

## Decision
Keep the *durability guarantee* of a real broker-backed design — write the
event to an outbox table in the same transaction as the business write, so
a domain write and "this needs publishing" can never diverge even if the
process crashes immediately after — but swap the delivery mechanism for an
in-process one: `OutboxDispatcherBackgroundService` polls the outbox every
200ms and hands each row to `InProcessEventDispatcher`, a plain in-memory
router, instead of a real relay publishing to Service Bus.

Every module depends only on `IIntegrationEventPublisher` (an interface in
`SharedKernel`), never on `InProcessEventDispatcher` directly. Swapping the
in-process dispatcher for a real Service Bus relay is a **host-only change**
— two lines in `Program.cs` — with zero changes required in any module.

## Alternatives considered

**1. Real Azure Service Bus (topic + subscriptions), wired in from day one.**
Rejected as the default path: costs real money at Standard tier, requires
provisioning before the app can even start, and — proven out by what
actually happened — becomes a single point of failure for the entire
capstone the moment the subscription lapses. The Bicep module for it
(`servicebus.bicep`) was still written and `what-if`-validated, but gated
behind `deployServiceBus = false` by default for exactly this reason.

**2. Direct in-process calls, no outbox at all.**
Simpler — Ordering could just call Inventory's handler directly. Rejected
because it silently reintroduces the exact failure mode the outbox pattern
exists to prevent: if the process crashes between the DB write and the
direct call, the event is lost forever, with no record it was ever supposed
to happen. This would defeat the entire point of demonstrating reliable
inter-module messaging.

**3. In-process dispatcher + outbox (chosen).**
Keeps the crash-safety guarantee identical to the real-broker version,
removes the hard dependency on paid/live infrastructure, and keeps the
routing logic itself unchanged from what a real relay would do — the only
thing that's "fake" is the network hop.

## Consequences

**Gained:** zero-cost, fully local runnability; the design survived the
Azure subscription expiring without needing to change; identical
transactional guarantees to the real-broker version; a genuinely swappable
seam (interface + host-only wiring) rather than a hardcoded shortcut.

**Given up, and accepted as scaffold limitations:**
- No real network-hop realism, no independent module scaling, no
  process-level isolation between "API" and "worker" — they're one process.
- No dead-lettering yet. A message that fails every attempt now retries
  forever on every poll instead of being quarantined after N attempts —
  see the critique below and the follow-up this closed.
- No competing consumers across separate processes — the dispatcher is
  single-threaded per poll.

**Follow-up work this trade-off implied — closed:** `DispatchOneBatchAsync`
now wraps each message's dispatch in its own `try/catch` (PR:
`fix/outbox-per-message-error-isolation`). One bad message is logged and
left unprocessed for retry on the next poll, instead of propagating out of
`ExecuteAsync` and taking down the whole host — this was the sharpest gap
surfaced by outside review, described below.

**Still open, deliberately out of scope for that PR:** no dead-letter
equivalent yet, so a message that can *never* succeed retries forever
rather than eventually being quarantined. Needs a delivery-attempt counter
on `OutboxMessage` and a policy for what happens past the limit — real
follow-up work, not solved here.
