---
name: modular-monolith-testing
description: Use when writing or reviewing tests for a modular monolith, organizing test projects in a modular .NET solution, deciding whether a scenario needs a unit, integration, or end-to-end test, or testing module contracts and cross-module events.
version: 1.0.0
author: ardalis
---

# Modular Monolith Testing

Module boundaries are the unit of test design: test each module through its public contract, test cross-module behavior at the contract seam, and reserve E2E tests for a few critical workflows.

**Not for:** architecture/dependency-rule tests (`fitness-functions`), designing contracts (`inter-module-communication`), source layout (`modular-solution-layout`).

## Choosing the Test Type

A test is not a unit test if it touches a database, network, or file system, or can't run in parallel.

| Scenario | Type | Notes |
|---|---|---|
| Domain logic in one module | Unit | No infrastructure; parallel |
| Use case handler with in-module deps | Unit | Fake the module's own interfaces (repos, clock, email) |
| Handler calling another module | Unit | Fake the other module at its contract, never its internals |
| Module data access (EF queries, mappings, migrations) | Integration (module) | Real provider, one module's schema |
| Two modules via contract or event | Integration (cross-module) | Wire through the composition root; assert at the seam |
| Module HTTP endpoints | Integration (module) | Host the app, hit only that module's routes |
| Full workflow across modules and UI/API | E2E | Critical paths only |
| Dependency rule | Architecture | `fitness-functions` |

- Push tests down: many unit, fewer integration, few E2E.
- Within-module coverage belongs to the module; cross-module coverage belongs to the contract.
- If a unit test needs another module's internals to compile, a boundary is broken. Fix the boundary, not the test.

## Testing Contracts and Events

1. **Test through the contract.** Don't use `InternalsVisibleTo` to reach around it; needing it broadly means the contract is incomplete.
2. **Consumers fake the contract.** If Registration uses Catalog's `ICourseCatalog`, its unit tests use a fake `ICourseCatalog`, never Catalog internals.
3. **Providers verify they honor it.** Catalog's integration tests exercise the real implementation and assert what consumers rely on (nulls, ordering, errors).
4. **Test both sides of an event:** publisher raises it with correct data; subscriber given the event produces the outcome; plus one cross-module test proving the composition-root wiring.
5. **Contract changes fail provider tests first**, before any consumer breaks at runtime.

```csharp
[Fact]
public async Task CompletingCourse_RaisesCourseCompleted_WhichAwardsCertificate()
{
    using var app = TestHost.CreateWithModules("Registration", "Certificates");
    await app.SendAsync(new CompleteCourseCommand(StudentId, CourseId));
    var certs = await app.QueryAsync(new GetCertificatesQuery(StudentId));
    Assert.Contains(certs, c => c.CourseId == CourseId);
}
```

Only public commands, queries, and events: it survives rewrites of either module's internals.

## Test Project Organization

```text
tests/
  Modules/Registration/
    Registration.UnitTests/
    Registration.IntegrationTests/
  Solution/
    App.E2ETests/
    App.ArchitectureTests/
```

- One unit and one integration test project per module. Never reference another module's non-contract projects.
- Cross-module integration tests live with the consumer or in a shared integration project, referencing only contracts plus the composition root.
- E2E and architecture tests are solution-level.
- Mirror source folders (`UseCases/`, `Domain/`, `Endpoints/`); name tests by behavior; keep unit suites parallel, shared state goes to integration.

## Checklist

1. Classify with the table; use the cheapest level that proves the behavior.
2. Pick the project: `<Module>.UnitTests`, `<Module>.IntegrationTests`, or solution-level E2E.
3. No test references another module's internals.
4. Unit tests fake other modules at contracts; cross-module tests use real composition-root wiring.
5. Events: publisher, subscriber, and one wiring test.
6. Contract change: update provider contract tests first; confirm consumer fakes still match.
7. If a boundary made a test hard, file it and route enforcement to `fitness-functions`.

## Related Skills

`fitness-functions`, `inter-module-communication`, `modular-solution-layout`.
