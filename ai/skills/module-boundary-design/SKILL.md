---
name: module-boundary-design
description: Use when decomposing a system into modules, proposing modules for a new modular monolith, evaluating whether existing module boundaries are correct, deciding which module should own a new feature, or reviewing a design where a change repeatedly touches several modules. Also use when a shared kernel is proposed or has grown and its contents need review.
version: 1.0.0
author: ardalis
---

# Module Boundary Design

## Overview

Draw module boundaries along domain seams, not technical layers. A good boundary hides design decisions likely to change and exposes an interface much simpler than its implementation. Boundaries chosen well make most future changes local to one module.

## When to Use

- Decomposing a new or existing system into modules.
- Deciding where a new feature or entity belongs.
- Reviewing a proposed module list or an existing module map.
- Diagnosing why changes ripple across several modules.
- Evaluating what belongs in a shared kernel.

**When NOT to Use:**

- Measuring coupling or cohesion inside code you already have. Use `coupling-cohesion-analysis`.
- Laying out projects, folders, and registrations for agreed modules. Use `modular-solution-layout`.
- Choosing how modules talk to each other. Use `inter-module-communication`.
- Writing automated rules that enforce agreed boundaries. Use `fitness-functions`.

## What Makes a Good Module

A module is a unit of the system that owns one business capability. Judge a candidate module against these criteria:

- **Deep, not shallow.** The interface is much simpler than the implementation (Ousterhout). A module that only forwards calls adds coupling without hiding anything.
- **Hides decisions likely to change.** Information hiding (Parnas) is the decomposition criterion: put each hard or volatile design decision inside one module and keep it out of the interface.
- **High internal cohesion.** Everything inside changes for the same business reasons. If half the module changes with billing rules and half with scheduling rules, it is two modules.
- **Low external coupling.** The module depends on other modules only through their public contracts, never their internals.
- **Owns its data.** Each module owns its entities and persistence. No other module reads or writes its tables directly.
- **Names a business capability.** Good names come from the domain (Certification Tracking), not from layers (Services, DataAccess) or technology.
- **Independently understandable and testable.** A developer can work on the module while holding only its contracts to the rest of the system in mind.
- **Aligned with team ownership.** Conway's Law applies: module boundaries that cut across team boundaries produce constant coordination. Prefer one owning team per module (Team Topologies: start with the team, not the software).

## Finding Boundaries with DDD

Domain-Driven Design supplies the heuristics for where to cut:

1. **Listen for language shifts.** When the same word means different things to different stakeholders, you have found a context boundary. In a corporate training system, a "course" means curriculum content to course managers and a completion requirement to compliance officers. Different models, different modules.
2. **Map bounded contexts first, modules second.** A bounded context delimits where one model applies consistently (Evans). Default to one module per bounded context. Split only if a context is genuinely large; merge only if two contexts always change together and share one team.
3. **Classify subdomains.** Core subdomains deserve their own carefully designed modules. Supporting and generic subdomains can be simpler modules or bought capabilities. Do not spend boundary effort evenly.
4. **Let each context keep its own model of shared concepts.** A person is a User in User Management, a Learner in Course Management, and a Certificate Holder in Certification Tracking. Do not force one shared entity across contexts; translate identifiers and contracts at the boundary instead.
5. **Group by rate and reason for change.** Things that change together belong together. If a rule change always edits two candidate modules, reconsider the cut.
6. **Check transactional needs.** Operations that must be consistent in one transaction fit best inside one module. Boundaries that split an invariant force awkward cross-module coordination.
7. **Sanity-check against teams.** If the proposed map requires two teams inside one module, or one team spread across five modules, adjust the boundaries or flag the org mismatch.

## Enforcing the Boundary in .NET

Boundaries only hold when the code makes violations hard:

- Give each module its own project (or project set) with `internal` as the default visibility.
- Expose a small public contract surface: interfaces, DTOs, and events in a contracts project.
- Allow other modules to reference contracts only, never implementation projects.
- Automate these rules; see `fitness-functions` for ArchUnit.NET and NsDepCop specifics.

## The Shared Kernel Pattern

A shared kernel is a small set of types that multiple modules deliberately share and jointly own.

**Rules:**

- Keep it small. Include only truly universal, stable concepts: strongly typed IDs, common value objects (Money, DateRange), base abstractions, and cross-cutting primitives.
- Treat every change as a breaking change. Any modification requires agreement from all consuming modules, because it couples them all.
- Keep it free of business logic and entities. Domain behavior belongs to exactly one module.
- Prefer duplication over a bad abstraction. Two modules holding slightly different Address types is cheaper than one shared type that serves neither well.

**Limits:**

- The shared kernel is the highest-coupling element in the system. Every addition raises the change cost for every module.
- A growing kernel is a boundary smell. When teams add "convenient" types to avoid defining contracts, the kernel becomes a dumping ground and the monolith drifts toward a big ball of mud.
- If two modules need rich shared behavior, that behavior is probably its own module with a contract, not kernel material.

## Boundary Smells and Remedies

| Smell | What it indicates | Remedy |
|---|---|---|
| One feature routinely spans many modules | Boundaries cut across a cohesive capability | Redraw so the capability lives in one module; move the feature's core to a single owner |
| Chatty, fine-grained cross-module calls | Two modules share one underlying concern | Merge them, or move the talkative logic to the module that owns the data |
| Two modules always change in lockstep | Hidden shared model or split invariant | Merge, or extract the shared concept into an explicit contract or its own module |
| Module reads another module's tables | Data ownership boundary is fiction | Route access through the owner's contract; see `inter-module-communication` |
| Module names mirror layers (Core, Services, Data) | Technical decomposition, not domain decomposition | Re-partition by business capability and bounded context |
| Shared kernel keeps growing | Contracts are being bypassed | Audit the kernel; move logic to owning modules; keep only stable primitives |
| One giant module dwarfs the rest | Multiple contexts hiding under one name | Look for language shifts and change-reason clusters inside it; split along them |
| Many tiny pass-through modules | Shallow modules; interfaces add no hiding | Merge shallow modules into the capability they serve |
| Circular dependencies between modules | No clear owner for a shared concept | Assign ownership; invert one direction with a contract or event |

## Checklist

Use this when proposing or reviewing a module map:

1. List the business capabilities and subdomains; mark core vs supporting vs generic.
2. Identify bounded contexts by language shifts and stakeholder vocabulary.
3. Propose one module per bounded context; name each after its capability.
4. For each module, state in one sentence what it hides and what it owns (data and decisions).
5. Verify each module's interface is far simpler than its implementation.
6. Confirm every entity and table has exactly one owning module.
7. Walk three or four likely change scenarios; each should touch one module plus, at most, contract consumers.
8. Check invariants: no transaction spans module boundaries.
9. Map modules to teams; flag any module owned by no team or by several.
10. Review the shared kernel: only stable, logic-free primitives; everything else moved to an owner.
11. Scan the smells table above against the proposal; record findings with remedies.
12. Hand off: structure to `modular-solution-layout`, interactions to `inter-module-communication`, rules to `fitness-functions`.

## Related Skills

- `coupling-cohesion-analysis`: evaluates code-level coupling and cohesion once boundaries exist; this skill decides where the lines go.
- `modular-solution-layout`: turns an agreed module map into .NET solution, project, and registration structure.
- `inter-module-communication`: defines how modules exchange data and behavior across the boundaries drawn here.
- `fitness-functions`: automates the boundary rules with ArchUnit.NET, NsDepCop, and CI validation.
