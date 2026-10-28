# Reporting Across Modules

Reports only read, but that does not suspend boundaries. A report consumes only what a module exposes via its contract or publishes into a read model. If one module owns all the data, build the report inside that module.

## Reach-in anti-pattern
Looks like: a `ReportingService` DbContext mapping other modules' entities; raw SQL joining `Training.Enrollments` to `Hr.Employees`; a reporting project referencing module internals to reuse entities.

```csharp
// Anti-pattern
var rows = await _db.Database.SqlQueryRaw<ProgressRow>("""
    SELECT e.FullName, c.Title, en.PercentComplete
    FROM Hr.Employees e
    JOIN Training.Enrollments en ON en.EmployeeId = e.Id
    JOIN Training.Courses c ON c.Id = en.CourseId
    """).ToListAsync();
```

Tempting (same DB, quick, "reads can't break anything"). Harmful: owners can't rename, split, or change persistence without breaking hidden consumers; schema rules get reimplemented in SQL and drift; extraction becomes impossible; one tolerated reach-in sets precedent.

## Remedies (simplest that meets freshness needs)

1. **Compose over query contracts** (live data, small sets):
```csharp
public class EmployeeProgressReportService(
    IEmployeeQueryService hr, IEnrollmentQueryService training)
{
    public async Task<IReadOnlyList<ProgressRow>> GetReportAsync()
    {
        var employees = await hr.ListActiveEmployeesAsync();
        var progress = await training.GetProgressByEmployeeIdsAsync(employees.Select(e => e.Id));
        return employees.Join(progress, e => e.Id, p => p.EmployeeId,
            (e, p) => new ProgressRow(e.FullName, p.CourseTitle, p.PercentComplete)).ToList();
    }
}
```
2. **Reporting-owned read model**: denormalized tables in Reporting's own schema, fed by events. For large aggregations, history, and dashboards that tolerate staleness. Define the feeding events, the schema, and how to rebuild/backfill.
```csharp
public class EnrollmentCompletedHandler(ReportingDbContext db)
    : INotificationHandler<EnrollmentCompleted>
{
    public async Task Handle(EnrollmentCompleted e, CancellationToken ct)
    {
        db.CompletionFacts.Add(new CompletionFact(e.EmployeeId, e.CourseId, e.CompletedOn));
        await db.SaveChangesAsync(ct);
    }
}
```
3. **Module-owned report endpoints**: one module's data with labels from elsewhere (e.g. a compliance audit); enrich names via other modules' contracts.

Missing query on a contract? Ask the owner to add it; never bypass. Report queries are side-effect free on lean read paths (projections, `AsNoTracking`, Dapper). CQRS relaxes how a module reads its own data, never another's.

## Dashboards
- Each module exposes summary metrics (counts, rates, top-N) via its contract; each widget binds to one module's metrics or to the read model. Never one giant cross-module query.
- Cross-module KPIs (e.g. compliance % across departments) live in the read model, computed from events.
- Cache/precompute expensive aggregates in Reporting-owned tables; render other widgets if one module is slow or down.
- Add an architecture test forbidding Reporting from referencing module internals.
