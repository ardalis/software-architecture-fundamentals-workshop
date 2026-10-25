# Exercise: Find the Coupling and Cohesion Problems

**Module:** Laws and Principles (2-day module 2, 1-day module 2)
**Format:** pairs
**Time:** 2-day: 15 min + 10 min debrief. 1-day: 10 min + 5 min debrief.

## Goal

Spot coupling, connascence, and cohesion problems in real-looking code, and decide which ones are worth fixing first.

## Materials

[`OrderManager.cs`](./OrderManager.cs), a small piece of TicketTide's legacy checkout code. It's a .NET 10 file-based app:

```sh
cd exercises/coupling-cohesion
dotnet run OrderManager.cs
```

It writes an `orders.log` file in the current folder.

## Steps

1. **Run it and read it (3 min).** It works. Look at the output closely, though.
2. **Find the problems (7 min).** List as many problems as you can find. For each one, write:
   - the line or member
   - the kind of problem: afferent or efferent coupling, a connascence type (name, position, meaning, algorithm, timing, ...), or low cohesion
   - what change would hurt because of it
3. **Rank them (5 min).** Pick the **two** you would fix first and say why. Use connascence *strength*, *locality*, and *degree* to argue. Don't fix anything yet. The goal is to decide.

1-day: skip the ranking and pick just one to fix first.

## Debrief questions

- Which problem has already caused a bug? How did you notice?
- Which problems are only a concern because of *where* the coupled code lives (locality)?
- `OrderManager` has high efferent coupling. Which of its dependencies are stable, and which are volatile?
- If this code were split into modules, which members would go where?
