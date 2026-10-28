---
name: modular-solution-layout
description: Use when scaffolding a .NET modular monolith solution, adding or restructuring a module, wiring module registration in the composition root, reviewing project layout and naming, choosing Clean Architecture vs Vertical Slice inside a module, or when cross-cutting concerns (logging, validation, caching, authorization) leak into base classes or duplicated handler code.
version: 1.0.0
author: ardalis
---

# Modular Solution Layout

One deployable app; each module is a project pair hiding its internals behind a small Contracts project. The host is the only composition root, and project references encode the dependency rules.

**Not for:** deciding boundaries (`module-boundary-design`), cross-module calls/events (`inter-module-communication`).

## Layout and Naming

```
src/  {Product}.Web                 # host + composition root, no business logic
      {Product}.SharedKernel        # minimal shared base types only
      {Product}.{Module}            # internals (e.g. Users, Courses, Certifications)
      {Product}.{Module}.Contracts  # public surface
tests/{Product}.{Module}.Tests      # one per module, mirrors module folders
      {Product}.IntegrationTests
```

- Name modules after business capabilities (short plurals), never technical layers. Namespaces follow folders under `{Product}.{Module}.*`.
- Worked example (CorporateTraining: Users, Courses, Certifications) and registration code: [references/example-and-wiring.md](references/example-and-wiring.md).

## Module Anatomy

- **Contracts (public):** service interfaces, DTOs/records, integration events. No entities, DbContext, logic, or infrastructure deps.
- **Module (internal):** entities, logic, its own `DbContext` and tables/schema, handlers, endpoints, and `internal` implementations of the Contracts interfaces.
- Default every type to `internal`; `public` only if it belongs in Contracts. The compiler then enforces encapsulation.

## Composition Root

- Each module owns one `Add{Module}Module(IServiceCollection, IConfiguration)` extension (plus `Map{Module}Endpoints` if it has endpoints). `Program.cs` just calls them.
- The host holds only hosting, middleware, and registration; it never news up module internals.
- Callers inject Contracts interfaces, never concrete services.

## Dependency Rules (enforce via project references)

1. Only `Web` references module projects.
2. A module references its own Contracts, other modules' Contracts as needed, and SharedKernel.
3. Never module-to-module implementation references.
4. Contracts reference nothing except optionally SharedKernel.
5. SharedKernel references nothing; keep it minimal, not a dumping ground.
6. Each module owns its data; no one touches another module's `DbContext` or tables.
7. Test projects reference only the module under test (plus Contracts); integration tests may reference Web.

## Inside a Module: Choose Per Module

Modules can differ. The internal pattern never leaks past the contract, so switching is a module-local refactor.

| Factor | Clean Architecture | Vertical Slice |
|---|---|---|
| Domain | Rich invariants, many rules | Mostly CRUD, thin logic |
| Lifespan | Long-lived, strategic | Short-lived, peripheral |
| Change | Stable concepts, shifting infra | Frequent independent features |
| Team | Knows layered/DDD | Prefers feature folders, small |
| Testing | Heavy domain unit tests | Feature-level integration tests |
| Size | Many use cases sharing a model | Small, features rarely share logic |

- Default simple modules to Vertical Slice, complex core-domain modules to Clean Architecture. Revisit as complexity changes.
- **Clean Architecture:** domain core, use cases, infrastructure; dependencies point inward only. Prefer folders + `internal` over extra projects unless enforcement needs project boundaries.
- **Vertical Slice:** one folder per feature (request, handler, validation, data access, response); no mandatory skeleton; watch for business rules duplicating across slices.
- Detail: [references/internal-architecture.md](references/internal-architecture.md).

## Cross-Cutting Concerns

Use composition, not shared base classes: decorators (same interface wrapping an inner implementation) or mediator pipeline behaviors (chain of responsibility). One concern per decorator/behavior; register them in the module's own registration; keep them per module unless genuinely shared (then deliberate and minimal).

## Checklists

**New solution:** solution + `src/`/`tests/`; Web stripped to hosting; minimal SharedKernel; module + Contracts pair per boundary; references per rules; `Add{Module}Module` calls in `Program.cs`; test project per module + integration tests; architecture tests early (`fitness-functions`).

**Add a module:** confirm the boundary; create the pair with conventional names; `internal` by default; own persistence; registration extension + one `Program.cs` line; reference others only via Contracts; add tests and update architecture tests.

**Review internals:** pattern invisible outside the module; inward-only dependencies (CA); slices independent with no silent rule duplication (VSA); cross-cutting code out of base classes and copy-paste; flag heavy layering on trivial modules or rich domains scattered across slices.

## Related Skills

`module-boundary-design`, `inter-module-communication`, `coupling-cohesion-analysis`, `fitness-functions`.
