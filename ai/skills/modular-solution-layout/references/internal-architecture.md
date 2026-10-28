# Module Internal Architecture Detail

## Clean Architecture in a module
- **Domain core:** entities, value objects, domain events, domain services. No infrastructure/UI deps.
- **Use cases:** commands, queries, handlers orchestrating the domain. Depend only on the core.
- **Infrastructure:** EF Core, external services, files. Implements interfaces the inner layers define.
- Layers may be projects or folders; folders + `internal` keep the project count manageable with many modules.
- Strengths: testable core, clear home for rules, resilient to infra change. Cost: ceremony simple modules don't need.

## Vertical Slice in a module
- Each slice folder holds one feature end to end; minimize coupling between slices, maximize cohesion within.
- Each slice uses the simplest code that works; a complex slice can still use a rich domain model.
- Shared behavior goes in pipeline behaviors/decorators, not inheritance.
- Strengths: low ceremony, fast features, easy onboarding. Cost: rules scatter if the domain grows; monitor duplication.
- A slice module accumulating shared rules can grow a domain core; a shrunken layered module can flatten.

## Decorator example

```csharp
public class CachedCourseQueryService(ICourseQueryService inner, IMemoryCache cache)
    : ICourseQueryService
{
    public async Task<CourseDto?> GetByIdAsync(int id) =>
        await cache.GetOrCreateAsync($"course-{id}", _ => inner.GetByIdAsync(id));
}
```

Mediator pipeline behaviors (validate, authorize, log, transactions) give every slice or use case the same treatment without inheritance and serve both patterns. Avoid god wrappers.

## Recording the choice
State the module's purpose and complexity (CRUD/mixed/rich), lifespan and change rate, and team familiarity; apply the decision table; record the choice with a one-paragraph rationale.
