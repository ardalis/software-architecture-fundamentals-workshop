---
name: architecture-style-selection
description: Use when choosing an architecture style for a new system or validating an existing one; when a user asks whether to use a layered/Clean Architecture monolith, vertical slices, a modular monolith, service-based, event-driven, or microservices; when a team proposes a rewrite or a split into services; or when reviewing a proposal or ADR that picks a style.
version: 2.0.0
author: ardalis
---

# Architecture Style Selection

There is no best style, only the least-worst for this system's top characteristics, team, and
constraints. Score candidates with trade-off-analysis; this skill supplies the style trade-offs.

**Not for:** drawing module lines (module-boundary-design), laying out a chosen modular solution
(modular-solution-layout), or picking sync vs. async between parts (inter-module-communication).

## Styles

| Style | Partitioning / deploy | Good at | Costs |
|---|---|---|---|
| Layered (n-tier) | Technical; 1 unit | Simple, cheap, familiar | Features smeared across layers; drifts to big ball of mud |
| Clean Architecture | Technical, dependencies point inward; 1 unit | Testable core, swappable infrastructure | Ceremony and indirection for simple CRUD |
| Vertical slice | By feature; 1 unit | Low cost per feature, little shared abstraction | Slices can reach into each other; duplication; no domain boundaries by itself |
| Modular monolith | By domain, enforced boundaries; 1 unit | Modularity without network; cheap refactoring; extraction seams | Needs enforcement; scales and deploys as one |
| Service-based | A few coarse domain services, often a shared DB | Some independent deploy, modest ops cost | Shared DB couples schemas; partial distribution costs |
| Event-driven | Components react to events via a broker | Responsiveness, decoupled producers, elastic | Eventual consistency, hard debugging and error handling, ordering |
| Microservices | Many fine-grained services, each owns its data | Independent deploy/scale per service, team autonomy, fault isolation | Network calls, distributed transactions (sagas), high ops and testing cost |

The workshop's [samples](../../../samples/) implement these: `clean-architecture`, `vertical-slice`,
`atomic-modular-monolith`, and the `*-saga` microservice variants. The saga variants show how
communication (sync/async), consistency (atomic/eventual), and coordination
(orchestrated/choreographed) trade off.

## Decision rules

1. **Count quanta first.** If every part shares the same top characteristics, one deployable
   usually wins. Distribute only the parts whose characteristics really differ (scale,
   availability, deploy cadence, isolation).
2. **Distribution must be justified by a current need**, not anticipated scale. Without one, the
   default is a modular monolith (several domains) or a layered/Clean/vertical-slice app (one
   small domain).
3. **Modularity, not distribution, prevents mud.** Splitting a tangled monolith makes a
   distributed ball of mud. Modularize first.
4. **Internal style is a separate decision.** Clean Architecture vs. vertical slice is a choice
   about how to organize code within a deployable or module, not a system topology.
5. **Prefer reversible.** A modular monolith keeps extraction open; consolidating services is
   expensive. On a near tie, pick the option that's cheaper to undo.
6. **Consistency needs drive topology.** Steps that need atomic, all-or-nothing transactions
   argue for one deployable and one database. Accepting eventual consistency (sagas, events) is
   the price of splitting them.
7. **Hybrids are normal.** For example, a modular monolith with one extracted, independently
   scaled service, or event-driven integration between monolith modules.

## Checklist

1. List drivers: top 3 characteristics, team count, deploy cadence, scaling hot spots, consistency
   needs, ops maturity.
2. Group components into quanta by shared characteristics.
3. Shortlist 2–3 styles from the table and score them (trade-off-analysis).
4. Name what the choice gives up and what would trigger a revisit (e.g. "extract Payments if
   checkout load exceeds X").
5. Record it as an ADR (adr-writing), and add fitness functions that protect it
   (fitness-functions).

## Related skills

trade-off-analysis · module-boundary-design · modular-solution-layout · inter-module-communication ·
dependency-failure-resilience · fitness-functions · adr-writing
