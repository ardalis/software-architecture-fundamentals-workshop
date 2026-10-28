---
name: dependency-failure-resilience
description: Use when designing what happens when a database, API, payment provider, email service, or other dependency is slow or down; when adding timeouts, retries, circuit breakers, bulkheads, fallbacks, or queued writes; or when reviewing HttpClient resilience in .NET.
version: 1.0.0
author: ardalis
---

# Dependency Failure Resilience

## Overview

Every remote dependency will be slow or down at some point. Doing nothing is still a choice: usually a hung request and a confused user. Decide per dependency, for reads and writes separately, what the user sees, what you promise them honestly, and which architecture characteristic you trade away.

## When to Use

- Calling another service, a vendor API, or a database across the network.
- Designing behavior for an outage scenario ("payment provider times out for 3 minutes").
- Reviewing code that calls `HttpClient` with no timeout or resilience policy.

**When NOT to use:**

- In-process calls between modules of a monolith (use inter-module-communication).
- Choosing sync vs async messaging as an architecture style (use architecture-style-selection).

## Step 1: Classify the Failure

| Question | Why it matters |
|----------|---------------|
| Read, write, or both? | Reads can often use a cache; writes can often be queued |
| Sync (user is waiting) or async (background)? | Sync: you have seconds. Async: you have retries and time |
| What does the user see if you do nothing? | That is your baseline; usually a spinner then a 500 |
| Is the operation safe to repeat? | Decides whether retries and redelivery are safe at all |
| Who decides the business answer? | Staleness and "accept now, confirm later" are business calls |

## Step 2: Choose Strategies

| Strategy | Use for | Cost / risk |
|----------|---------|------------|
| Timeout | Every remote call, always | Too long ties up threads; too short fails healthy calls |
| Retry with exponential backoff + jitter | Transient faults on idempotent calls | Retry storms; duplicate side effects if not idempotent |
| Circuit breaker | Dependency is clearly down; stop hammering it | Fails fast for everyone while open |
| Bulkhead (concurrency limit) | Keep one slow dependency from exhausting shared resources | Rejects excess calls |
| Fallback: cached read | Reads that tolerate staleness | Stale data; tell the user how stale |
| Fallback: queued write | Writes that can complete later | Must handle later failure; needs idempotent handler |
| Fail fast with a clear message | Nothing safe to substitute | Lost availability, preserved correctness |

Order matters: total timeout > retry > circuit breaker > per-attempt timeout.

## Step 3: Idempotency for Retries and Queues

Retries and at-least-once delivery mean the handler **will** see duplicates.

- Send an idempotency key (order ID) to vendors that support it (most payment APIs do).
- Record processed message IDs; skip ones already handled (inbox pattern).
- Use the outbox pattern so DB change and message publish cannot diverge.
- Ask: "what breaks if this is processed twice?" Charging a card twice is the classic answer.

## Step 4: Be Honest With the User

"Your order is confirmed" and "We received your order" are different promises. If a write is queued, say so and notify on completion or failure. If a read is cached, show its age.

## TicketTide Scenarios

| Scenario | Type | Reasonable response | Trading away |
|----------|------|---------------------|--------------|
| Seat map service down | Read | Serve cached map, mark as possibly stale | Consistency |
| Payment provider timing out during on-sale | Write, sync | Short timeout, limited retries with idempotency key, breaker; hold seats and show "payment pending" or fail fast | Availability or simplicity |
| Reporting DB unreachable | Read | Last snapshot with timestamp, or clear error | Freshness |
| Email provider rejecting | Write, async | Queue and retry with backoff; resend later; ticket visible in account | Timeliness |
| Door scanner offline | Read + write | Pre-downloaded ticket list, scan offline, sync later; accept duplicate-entry risk | Consistency |

The CAP/PACELC choice can differ per scenario in the same system.

## .NET Guidance

Prefer `Microsoft.Extensions.Http.Resilience` (built on Polly v8) over hand-rolled retry loops. Start with `AddStandardResilienceHandler()` on typed clients, then tune. Inject the typed client behind an interface the caller owns. See [references/dotnet-resilience.md](references/dotnet-resilience.md).

## Verify With Fitness Functions

Resilience you never test is a guess. Use fitness-functions to:

- Fail the build if a registered `HttpClient` has no resilience handler.
- Run fault-injection tests (Polly chaos strategies or a stub that returns 503/timeouts) asserting the fallback and user message.
- Assert duplicate messages produce one effect.
- Track p99 latency and breaker-open events against the measurable targets from trade-off-analysis.

## Checklist

- [ ] Each dependency classified: read/write, sync/async
- [ ] Every remote call has a timeout
- [ ] Retries only on idempotent or keyed operations, with backoff and jitter
- [ ] Circuit breaker on dependencies that can go fully down
- [ ] Fallback chosen and its staleness or delay communicated to users
- [ ] Queued writes have idempotent handlers and a failure path
- [ ] Trade-off named and recorded (adr-writing)
- [ ] Failure behavior covered by tests or fitness functions

## Common Mistakes

- No timeout, so the default 100 s `HttpClient` timeout hangs the request.
- Retrying non-idempotent writes and double-charging.
- Retries at every layer multiplying load on a struggling dependency.
- Telling users "confirmed" when the work is only queued.

## Related Skills

fitness-functions, trade-off-analysis, inter-module-communication, build-vs-buy, adr-writing, modular-monolith-testing
