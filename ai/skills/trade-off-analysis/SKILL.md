---
name: trade-off-analysis
description: Use when turning requirements into architecture characteristics, choosing which "-ilities" matter most, setting measurable targets, or comparing candidate architectures (for example modular monolith vs microservices) and recording which one wins and why.
version: 1.0.0
author: ardalis
---

# Trade-off Analysis

## Overview

Everything in software architecture is a trade-off. There is no best architecture, only the least-worst one for *these* characteristics, *this* team, and *this* budget. Make the trade-off explicit: find the characteristics that matter, rank them, score the candidates honestly, and write down what you accept as a consequence.

## When to Use

- Reading a brief or requirements and asking "what quality attributes does this need?"
- Two or more reasonable architectures are on the table and the team is arguing.
- A decision needs a defensible rationale that will outlive the meeting.

**When NOT to use:**

- You know the characteristics and need a style catalog (use architecture-style-selection).
- The decision is about one vendor or component (use build-vs-buy).
- You need to enforce a chosen target in CI (use fitness-functions).

## Step 1: Find Characteristics

Highlight anything in the requirements that is a quality requirement, not a feature. Quote the source and tag it.

| Tag | Meaning | Example signal |
|-----|---------|----------------|
| Explicit | A stakeholder asked for it | "40,000 fans arrive in the first 5 minutes" -> elasticity |
| Implicit | Nobody said it, but missing it is a failure | Payments imply security; door scanning implies fault tolerance |

No quote, no evidence. Common candidates: availability, scalability, elasticity, performance, fault tolerance, data consistency, security, maintainability, testability, deployability, extensibility, observability, cost, customizability, simplicity.

## Step 2: Pick the Top Three

- **No more than three.** Supporting every characteristic produces a system that supports none well.
- Name at least one characteristic a stakeholder cares about that you are **not** prioritizing, why, and what you will do instead.
- Ask whether each one applies to the whole system or only part of it (checkout vs reporting). Different needs in different parts suggest separate architecture quanta.

## Step 3: Make Them Measurable

A characteristic without a target is an opinion.

| Vague | Measurable |
|-------|-----------|
| "Fast checkout" | Checkout p99 < 2 s with 40,000 arrivals in 5 min |
| "Highly available" | 99.9% monthly for purchase flow; scanning works fully offline |
| "Maintainable" | No module references another module's internals (checked in CI) |

Flag targets that are hard to test before go-live; they need load tests or fitness-functions.

## Step 4: Score the Candidates

1. Weight the top three 3, 2, 1. Add **cost** and **simplicity** at weight 1 if missing.
2. Score each candidate 1-5 per characteristic, with **one sentence of justification per score**.
3. Multiply and total.
4. **Sanity-check.** If the winner feels wrong, find the wrong weight or score. Change it only if you can explain why.
5. Find the **flip point**: which single score, changed by one star, flips the decision? That is your biggest risk.

| Characteristic | Wt | A | A x Wt | B | B x Wt | Why |
|----------------|:--:|:-:|:------:|:-:|:------:|-----|
| Elasticity | 3 | 3 | 9 | 5 | 15 | B autoscales; A relies on a waiting room and pre-scaling |
| ... | | | | | | |
| Cost | 1 | 5 | 5 | 2 | 2 | ~$4k vs ~$11k per month |
| Simplicity | 1 | 5 | 5 | 2 | 2 | 1 deployable vs 6 plus a broker |

Include rough numbers: hosting cost, time to first release, deployable units, new skills the team needs. They often decide more than the stars.

## Step 5: Consider Hybrids

Ask "A now, extract X later?" State what would have to be true to trigger the extraction, and make that trigger observable.

## Step 6: Record the Decision

Write the statement, then route to adr-writing:

> We chose ___ because ___. We accept ___ as a consequence.

Keep the scoring table as the rationale and list the rejected candidates as alternatives.

## Checklist

- [ ] Every characteristic has a quote and an explicit/implicit tag
- [ ] At most three prioritized; at least one consciously not prioritized
- [ ] Each priority has a testable target
- [ ] Every score has a one-sentence justification
- [ ] Cost and simplicity were scored
- [ ] Flip point identified
- [ ] Consequences accepted in writing (ADR)

## Common Mistakes

- Choosing the style first and back-filling characteristics.
- Scoring from reputation ("microservices = scalable") instead of from this design.
- Ignoring team skills and time to first release.
- Treating the spreadsheet total as the decision instead of input to judgment.

## Related Skills

adr-writing, architecture-style-selection, fitness-functions, module-boundary-design, build-vs-buy
