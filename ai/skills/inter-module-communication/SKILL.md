---
name: inter-module-communication
description: Use when one module needs data or behavior from another module in a modular monolith, when defining or reviewing module contracts, when choosing between synchronous calls and events for a cross-module interaction, when building reports or dashboards that span modules, or when a change adds a project reference, query, or type dependency that crosses a module boundary.
version: 1.0.0
author: ardalis
---

# Inter-Module Communication

Modules communicate only through explicit contracts, never through each other's implementations. Every cross-module interaction is a deliberate choice between a synchronous contract call and an asynchronous event.

**Not for:** calls within one module (internal code may call internal code), or external systems/separately deployed services.

## Scenario to Mechanism

| Scenario | Mechanism |
|---|---|
| A needs a value from B to finish the current operation | Sync query via B's contract |
| A must trigger behavior in B and needs the result now | Sync command/service call via B's contract |
| A wants B (and others) to react but doesn't depend on the outcome | Publish an event; B subscribes |
| Something notable happened others may care about | Publish an event; don't ask who listens |
| A needs a small, stable projection of B's data per request | Query B's contract, or a read model B exposes |
| Workflow spans several modules | Orchestrate from the caller via contracts; connect steps with events where ordering allows |
| A "just needs one column" from B's table | Still a contract call or event. Never a direct query |

- Default to events when the caller needs no answer; the publisher stays ignorant of consumers.
- Use sync calls only when the caller can't proceed without the result; make the temporal coupling visible in the contract.
- Every sync call is a dependency arrow; keep the graph acyclic. If A calls B and B needs A, replace one direction with an event.

## Commands, Queries, Events

| Type | Intent | Handlers | Returns | Dependency |
|---|---|---|---|---|
| Command (`EnrollUserInCourse`) | Do something | Exactly one | Result/ack | Sender on receiver's contract |
| Query (`GetCourseSummary`) | Give me data | Exactly one | DTO | Sender on receiver's contract |
| Event (`UserRegistered`) | Something happened | Zero or more | None | Receivers on publisher's contract |

Event payloads stay small and factual: what happened plus ids; subscribers query for more.

## Contracts

- One public surface per module: a small `{Module}.Contracts` project; everything else `internal`.
- Contains interfaces, DTOs, commands, queries, event records. No entities, EF types, or logic.
- Contracts never depend on implementation projects; consumers reference only other modules' Contracts.
- DTOs are the consumer-facing translation, not the domain model. Treat contracts as versioned APIs; keep them minimal.
- **Style per interaction:** service interfaces + DTOs for queries and request/response (consumers inject the interface); events via mediator for notifications and side effects. Don't build both styles for one interaction.
- **Mediator:** message types live in the publisher's Contracts; handlers live in the subscriber and stay `internal`. Use it at boundaries only; it hides the call graph, so back it with naming conventions and architecture tests.
- Examples and design notes: [references/contracts-and-mediator.md](references/contracts-and-mediator.md).

## Data Access

- Each module owns its data: own schema (or prefix), own DbContext, no shared entity types.
- Read another module's data via its query contract, or keep a local event-fed copy of the few fields you need (eventual consistency in-process is acceptable).
- Change another module's data by sending it a command; the owner enforces invariants.
- Transactions never span modules. Publish an event and let the second module react, or reconsider the boundary (`module-boundary-design`).

## Reporting Across Modules

Reports read, but boundaries still hold. If one module owns all the data, report inside it. Otherwise:

- Never join across module-owned tables or reach into another module's entities, repositories, or DbContext, even "read-only".
- **Live, small results:** compose in memory over each module's query contracts. Missing query? Ask the owner to add it.
- **Heavy aggregation, history, staleness OK:** a Reporting-owned read model in its own schema, fed by events.
- **Dashboards:** compose per-module widgets over each module's summary-metric contracts plus read-model KPIs; never one mega-query; tolerate one module being down.
- Keep report queries side-effect free on lean read paths. Detail and examples: [references/reporting.md](references/reporting.md).

## Never

- Reference another module's implementation project, or make implementation types `public` for another module.
- Query another module's tables, views, or DbContext. No reach-in SQL, shared EF models, or "just this one join".
- Expose entities, EF types, or internal services in Contracts, or let Contracts depend on an implementation.
- Create module dependency cycles, or share a transaction, connection, or unit of work across modules.
- Let a subscriber's failure silently corrupt the publisher's operation; isolate failures at the boundary.
- Bypass the contract "temporarily". Temporary reach-ins become permanent.

## Checklist

1. Caller needs an answer now? Command/query. Otherwise event.
2. Message or interface lives in the owner's Contracts; consumer references only Contracts.
3. Payloads are DTOs/records; naming is imperative/question/past tense.
4. Handlers `internal`: one per command/query, zero or more per event.
5. No query touches tables outside the module's schema; no unit of work spans modules.
6. Draw the new arrows; the module graph stays acyclic.
7. Add or update an architecture test for any rule this change relies on.

## Related Skills

`module-boundary-design` (chatty or cyclic communication signals a wrong boundary), `fitness-functions`, `modular-monolith-testing`.
