---
title: ADR 042 - Outbox Pattern for Order Event Publishing
weight: 42
---

<!--
Sample ADR for a fictional order-management application ("Ordo"). It demonstrates the full
section structure, the Options Considered format, code-grounded context, and honest consequences.
Everything referenced (classes, issues, packages) is invented for illustration. A real ADR does
not include this comment.
-->

- **Date:** 2026-08-06
- **Status:** Proposed
- **Participants:** SteveS, JaneD, BrendanE

## Context

[Issue #317](https://example.com/ordo/issues/317) reports that downstream services occasionally
miss `OrderPlaced` events: an order row exists in PostgreSQL but no event was ever published to
the message bus. An audit of the current codebase confirmed why — event publishing happens
**after** the database transaction commits, as a separate, unguarded step:

```csharp
await _dbContext.SaveChangesAsync(ct);        // order persisted
await _bus.Publish(new OrderPlaced(order.Id), ct);  // process crash here = event lost
```

This dual-write shape appears in three places today: `PlaceOrderHandler`, `CancelOrderHandler`,
and `RefundService.IssueRefundAsync`. There is no retry, no persistence of the pending event, and
no reconciliation job. The codebase has no existing outbox, inbox, or message-deduplication
infrastructure of any kind.

## Problem Statement

How do we guarantee that every committed order state change eventually produces exactly one
corresponding bus event, even if the process crashes between the database commit and the publish
call?

## Decision

Adopt a transactional outbox:

1. Handlers write pending events to an `outbox_messages` table **in the same transaction** as the
   order change, via a new `IOutbox.Enqueue(...)` abstraction in `Ordo.Core`.
2. A hosted background service (`OutboxDispatcher` in `Ordo.Infrastructure`) polls the table,
   publishes each message to the bus, and marks it dispatched. Rows are dispatched in insertion
   order per aggregate.
3. The three existing publish sites are migrated to `IOutbox.Enqueue`; direct `_bus.Publish` calls
   from request handlers are removed.

## Options Considered

### Option 1: Keep publish-after-commit, add retries (status quo plus)

Wrap the existing `_bus.Publish` calls in a retry policy.

**Rejected because:** retries only cover transient bus failures, not the crash window between
commit and publish that Issue #317 actually describes. The event still exists only in process
memory until the publish succeeds.

### Option 2: Adopt a full messaging framework with built-in outbox

Bring in a framework that ships outbox support out of the box.

**Rejected (for now) because:** no such framework is currently a dependency anywhere in the
solution (the package manifest has no entry for one), and the only capability needed today is
"persist event with the commit, publish later." Adopting a framework solely for its outbox slice
is a large dependency for a small need. If the messaging surface grows, a framework's outbox is a
drop-in replacement for the dispatcher — the `IOutbox` contract on handlers would not change.

### Option 3: Change data capture (CDC) on the orders table

Stream the database's write-ahead log and derive events from row changes.

**Rejected because:** it adds operational infrastructure (connector deployment, log-position
management) the team doesn't run today, and row-diffs cannot express intent — an `UPDATE` to
`status` can't distinguish "cancelled by customer" from "cancelled by fraud review", which
downstream consumers need.

### Option 4: Hand-rolled transactional outbox (chosen)

A small, dependency-free mechanism: one table, one `IOutbox` interface in `Ordo.Core`, one hosted
dispatcher in `Ordo.Infrastructure`. Roughly 150 lines, all independently testable.

## Rationale

The outbox makes the database commit the single source of truth: if the transaction committed, the
event is durably queued; if it rolled back, no event exists. The crash window from Issue #317 is
eliminated rather than shrunk. Keeping the mechanism hand-rolled matches the actual requirement
(three call sites, one bus) and leaves the migration path to a framework open, since only the
dispatcher — not the handler-facing contract — would be replaced.

Deliberately **not** unified here: the existing scheduled-report emails in `ReportMailer`, which
are fire-and-forget and tolerate loss. Routing them through the outbox would add ordering and
retention concerns for no correctness gain.

## Consequences

### Positive

- A committed order change can no longer lose its event; the failure mode becomes "delayed", which
  the dispatcher's retry loop resolves without human intervention.
- Handlers no longer talk to the bus directly, so unit tests assert against `IOutbox.Enqueue`
  instead of mocking bus internals.
- No new package dependency.

### Negative

- Events are now published with polling latency (up to the dispatcher interval, default 2s) rather
  than immediately. Acceptable for order events; revisit if a near-real-time consumer appears.
- Consumers can receive duplicates (dispatcher crash after publish, before marking dispatched), so
  every consumer must be idempotent. Mitigated by the `MessageId` header consumers already ignore
  or can dedupe on.
- The `outbox_messages` table grows unboundedly without cleanup; a retention job must ship in the
  same release.

## Open Issues

- Whether dispatch order must be global or only per-aggregate; the current design guarantees only
  per-aggregate ordering.

## Future Considerations

- If cross-cutting behavior (tracing, claim-check for large payloads) becomes necessary, replace
  `OutboxDispatcher` with a messaging framework's outbox without changing `IOutbox`.
- An inbox table on the consumer side is the natural fast-follow once duplicate-delivery metrics
  are visible in production.

## Related Links

- [Issue #317 - Missed OrderPlaced events](https://example.com/ordo/issues/317)
- [Transactional outbox pattern](https://microservices.io/patterns/data/transactional-outbox.html)
