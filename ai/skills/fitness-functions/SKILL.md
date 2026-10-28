---
name: fitness-functions
description: Use when turning an architectural decision or characteristic into an automated check; when writing NetArchTest or ArchUnit.NET rules, NsDepCop config, coupling metrics, performance budgets, or dependency/security scans; when a module boundary violation is found and must not recur; when a new module needs dependency rules; when architecture tests or fitness functions fail in CI; or when boundaries are enforced by convention alone.
version: 2.0.0
author: ardalis
---

# Fitness Functions

## Overview

A fitness function is an objective, automated check of an architecture characteristic. Decisions that live only in ADRs, docs, or heads decay with every commit, by humans and AI agents alike. Express each important decision as code that fails the build when someone breaks it.

## When NOT to Use

- Deciding where boundaries belong: use module-boundary-design first.
- Choosing communication patterns or contracts: use inter-module-communication.
- Functional unit, integration, or end-to-end tests: use modular-monolith-testing.

## Kinds

| Kind | Checks | Tools |
|---|---|---|
| Structural / dependency | Layer and module dependency direction, naming, placement ("classes implementing `IOrderRepository` reside in Infrastructure"), no cycles | NetArchTest, ArchUnit.NET, NsDepCop, project references |
| Coupling / cohesion metrics | Afferent/efferent coupling, instability, cycles, size, complexity thresholds | NDepend, analyzers, coupling-cohesion-analysis |
| Performance / latency | Budgets on hot paths or endpoints ("100,000 `PriceCalculator.Total` calls under 200 ms") | `Stopwatch` checks, BenchmarkDotNet, load tests |
| Security / dependency | Vulnerable or deprecated packages, licenses, secrets | `dotnet list package --vulnerable`, Dependabot, secret scanning |

Classify each one on two axes:

- **Atomic vs holistic:** atomic checks one characteristic in isolation (a dependency rule); holistic checks several interacting (latency under load with security on).
- **Triggered vs continuous:** triggered runs on an event (build, PR, deploy); continuous (continual) runs all the time, typically against production (monitoring, SLO alerts).

Prefer cheap, deterministic, atomic, triggered checks in the build. Give noisy ones (timing) warm-up, repeated runs, generous margins, or a dedicated perf stage.

## Writing One

1. Name the decision it protects, ideally an ADR: `// ADR-0003: Core has no infrastructure dependencies`.
2. Pick the cheapest tool that can express it objectively: project references beat tests; tests beat review.
3. Give the rule a readable name and reason, and report the offending types.
4. Prove it works: run it against a deliberate violation, watch it fail, revert.
5. Wire it into CI so failure fails the build (non-zero exit code, `dotnet test`, or analyzer error).

Exercise shape (NetArchTest in a file-based app) and typical fixes: `references/netarchtest.md`.

## Module Boundary Rules (Modular Monolith)

| Rule | Statement |
|---|---|
| Module isolation | A module's internals depend on nothing in another module's internals |
| Contract-only references | Cross-module references target only the other module's Contracts |
| No reach-in data access | No module references another module's entities, DbContext, or repositories |
| Dependency direction | Host references modules; modules never reference the host or each other's implementations |
| Contract independence | Contracts depend on no module implementation |
| No cycles | No circular dependencies between modules or namespaces within one |

Layer the enforcement: project references first (the compiler blocks most violations); NsDepCop for fast compile-time namespace rules; NetArchTest or ArchUnit.NET for richer rules (data reach-in, naming, type shape). Generate pairwise module rules from a module list as modules grow. Code: `references/archunitnet.md`, `references/nsdepcop.md`.

## Running in CI

- Architecture tests run in the normal `dotnet test` step; NsDepCop with `CodeIssueKind="Error"` fails `dotnet build`; a standalone fitness app fails via exit code 1.
- Gate PRs on them so a violation shows up in the PR that introduces it.
- Keep them fast and deterministic so nobody is tempted to skip them.
- Adopting with existing violations: start as warnings or an explicit, dated exception list, then ratchet to errors.

## When One Fails: Fix the Code, Not the Rule

A failing fitness function is the architecture working. Never delete, weaken, skip, or mute a rule to get green. Agents are prone to this shortcut, and it is easy to miss in a large diff; review changes to rule files explicitly (CODEOWNERS on the rules helps).

The only legitimate reason to change a rule is that the decision changed: write or supersede the ADR first, then update the rule in the same change, referencing the ADR.

**Standing rule for agents:** when you fix a boundary violation, add the rule that would have caught it in the same change.

## Fix-a-Violation Checklist

1. Identify the rule and the failing types it names.
2. Refactor toward a boundary-preserving design: inject an interface instead of newing a concrete class (DIP), call the other module's contract, let a module own its input type, or raise an event instead of calling back (breaks cycles).
3. Add or tighten the rule for this violation class.
4. Confirm it fails against the old code and passes now.
5. Ship fix and rule together.

## Related Skills

- module-boundary-design: decides the boundaries this skill enforces.
- inter-module-communication: patterns to refactor violations toward.
- coupling-cohesion-analysis: finds problems worth turning into rules.
- modular-monolith-testing: broader test strategy; architecture tests are one layer.
