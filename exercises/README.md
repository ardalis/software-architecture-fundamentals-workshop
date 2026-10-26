# Exercises

Hands-on exercises and discussions for the workshop, in the order they run. Most of them use **TicketTide**, a fictional ticketing company described in the [case study brief](./tickettide/brief.md), so decisions made in one exercise carry into the next.

Each exercise doc has the goal, materials, steps, and debrief questions. Times are team work + debrief.

| Exercise | 1-day | 2-day | Format | Materials |
| --- | --- | --- | --- | --- |
| [Exercise 0: set up your machine](./setup.md) | Before the workshop · M1 5 min check | Before the workshop · M1 5 min check | On your own | Your laptop |
| [Find the coupling and cohesion problems](./coupling-cohesion/) | M2 · 10 + 5 min | M2 · 15 + 10 min | Pairs | Runnable C# |
| [Architecture kata](./architecture-kata/) | M3 · 15 + 5 min | M3 · 20 + 10 min | Teams | [Brief](./tickettide/brief.md), [worksheet](./architecture-kata/characteristics-worksheet.md) |
| [Compare two candidate solutions](./compare-candidates/) | M3 · 10 + 5 min | M4 · 20 + 10 min | Teams | [Candidates](./compare-candidates/candidate-architectures.md) |
| [Add the same feature to two samples](./add-a-feature.md) | M4 · 20 + 5 min | M5 · 30 + 10 min | Pairs | [Clean Architecture](../samples/clean-architecture/) and [Vertical Slice](../samples/vertical-slice/) samples |
| [Refactor inline concerns into the pipeline](./pipeline-refactor/) | — | M6 · 25 + 10 min | Pairs | Runnable C# |
| [Handling dependency failures](./dependency-failures.md) | M5 · 8 + 5 min | M7 · 12 + 8 min | Groups | Discussion |
| [Find TicketTide's bounded contexts](./bounded-contexts.md) | — | M8 · 15 + 10 min | Teams | Whiteboard |
| [Split or merge?](./split-or-merge.md) | M7 · 12 + 5 min | M9 · 20 + 10 min | Teams | Scenario |
| [Write an ADR](./write-an-adr.md) | M8 · 12 + 3 min | M10 · 20 + 5 min | Teams | [ADR template](./templates/adr-template.md) |
| [Break a dependency cycle](./fitness-functions/#optional-exercise-break-a-dependency-cycle) (optional) | — | M11 · 20 min | Pairs | Runnable C# |
| [Write fitness functions](./fitness-functions/) | M9 · 20 + 5 min | M13 · 30 + 10 min | Pairs | Runnable C# |
| [Would you build it now?](./build-vs-buy.md) | — | M14 · 10 min | Whole room | Discussion |

## Running the code exercises

The code exercises are .NET 10 [file-based apps](https://learn.microsoft.com/dotnet/core/sdk/file-based-apps). Each is a single `.cs` file with no project file. Run one from its folder:

```sh
cd exercises/fitness-functions
dotnet run fitness.cs
```

The first run restores packages and takes a little longer.
