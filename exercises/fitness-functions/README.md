# Exercise: Write Fitness Functions

**Module:** Fitness Functions in Depth (2-day module 13), Governance and Fitness Functions (1-day module 9)
**Format:** pairs
**Time:** 2-day: 30 min + 10 min debrief. 1-day: 20 min + 5 min debrief (rules 1 and 2 only).

The same code is used for the optional [dependency cycle exercise](#optional-exercise-break-a-dependency-cycle) in 2-day module 11.

## Goal

Turn architectural decisions into automated checks that fail when someone, human or AI agent, breaks them. Then fix the code they catch.

## Materials

[`fitness.cs`](./fitness.cs) is a .NET 10 file-based app. The top half runs fitness functions with [NetArchTest](https://github.com/BenMorris/NetArchTest) against its own code. The bottom half is a slice of TicketTide in four namespaces:

| Namespace | Intended role |
| --- | --- |
| `TicketTide.Core.Orders` | Domain logic for orders |
| `TicketTide.Core.Billing` | Domain logic for invoices |
| `TicketTide.Infrastructure` | Data access implementations |
| `TicketTide.Web` | Endpoints |

```sh
cd exercises/fitness-functions
dotnet run fitness.cs
```

The app exits with code 1 if any fitness function fails, so it could run as a CI step as-is.

## Steps

1. **Run it (2 min).** The one example rule passes.
2. **Write rules for these decisions (15 min).** Add each one to the "Your fitness functions" section. Copy the example's shape.
   1. *Core does not depend on Infrastructure* (the dependency rule from Clean Architecture).
   2. *Classes implementing `IOrderRepository` reside in `TicketTide.Infrastructure`.*
   3. *Billing does not depend on Orders.* Orders may call Billing, but not the reverse.
   4. *Pricing stays fast.* Time 100,000 calls to `PriceCalculator.Total` with a `Stopwatch` and use `CheckThat` to require under 200 ms.
3. **Run again (1 min).** Which rules fail, and which types does each failure name?
4. **Fix the code, not the rules (10 min).** Make every rule pass without deleting or weakening a rule.
5. **Write the ADR link (2 min).** For one rule, write a comment naming the decision it protects, for example `// ADR-0003: Core has no infrastructure dependencies`.

1-day: do rules 1 and 2, then fix the code they catch.

## Debrief questions

- Which of your rules are *atomic* and which are *holistic*? Which are *triggered* and which could run *continually*?
- The performance check passes on your laptop. Would it be reliable in CI? How would you make it less noisy?
- If an AI agent "fixed" a failing rule by editing the rule, would you notice? What would stop it?
- What decision in your own codebase would you protect first?

## Optional exercise: break a dependency cycle

**Module:** Dependency Management (2-day module 11)
**Time:** 20 min, if time allows

1. Run `fitness.cs` and draw the dependencies between the four namespaces on paper or in a d3 sketch. Where is the cycle?
2. Add rule 3 above to make the cycle fail the build.
3. Break the cycle in two different ways: (a) Billing owns its input type, (b) Orders raises an event that Billing handles. Which one keeps the code simpler? Which one would survive extracting Billing into its own service?
