---
name: coupling-cohesion-analysis
description: Use when reviewing a diff or codebase for design quality, evaluating a proposed refactor, diagnosing why code is hard to change or test, or assessing whether modules in a .NET solution are too entangled to evolve independently.
version: 1.0.0
author: ardalis
---

# Coupling and Cohesion Analysis

## Overview

The connections between modules are the assumptions the modules make about each other (Parnas, 1972). Every review question reduces to two measures: how much does this code depend on things outside itself (coupling), and how well do the things inside it belong together (cohesion)? Minimize the first, maximize the second, and hide everything that callers do not need.

## When to Use

- Reviewing a pull request or diff for design quality.
- Evaluating whether a refactor actually improved the design.
- Diagnosing a codebase where small changes ripple across many projects.
- Deciding whether a class, project, or module has the right responsibilities.
- Assessing module boundaries in a modular monolith before enforcing them.

**When NOT to use:**

- Deciding where module boundaries should go in the first place (use module-boundary-design).
- Choosing how modules should talk to each other (use inter-module-communication).
- Throwaway scripts, spikes, or generated code that will not be maintained.

## Coupling Types: What to Look For

Review the diff against each row. Later rows are code-level smells; the first two are structural metrics.

| Type | What it looks like in code | Why it hurts | What to recommend |
|------|---------------------------|--------------|-------------------|
| Afferent (incoming) | Many projects or classes reference this one; changing it breaks callers everywhere | High blast radius; the type is frozen by its consumers | Keep the public surface small and stable; extract a narrow interface or contract for callers |
| Efferent (outgoing) | This class references many other namespaces; a long `using` list; a constructor with many injected services | The class breaks when any dependency changes; hard to test in isolation | Split responsibilities; depend on abstractions; question each dependency |
| Temporal | Methods that must run in a fixed order (`Init()` before `Process()`); shared static state set by one caller and read by another; two operations that must happen together to leave the system valid | Hidden ordering assumptions; works in one flow, fails in another; race-prone | Make the sequence explicit: constructor-enforced initialization, one method that performs the whole operation, or a returned value that the next step requires |
| Data | Methods pass only the simple values they need (`GetOrder(int orderId)`) | Mildest form; acceptable | Prefer this form; no action needed |
| Stamp | Passing a whole entity or DTO when the method uses one or two fields; handing another module your EF entity | Callee is coupled to a structure it barely uses; changes to the type ripple; internal types leak across boundaries | Pass just the fields needed, or define a purpose-built parameter or contract type |
| Control | Boolean or enum flags that switch the callee's behavior (`Save(order, sendEmail: true)`); callers that know the callee's internal branches | Caller must understand callee internals; every flag doubles the paths to test | Split into intention-revealing methods; move the decision into the callee or a strategy |

Cross-module rule: within a class, data coupling is fine. Across module boundaries, any coupling to another module's internal types (stamp coupling to its entities, control flags into its services) is a boundary violation. Modules should share contracts, not internals.

## Measuring Coupling

- **Afferent coupling (Ca):** count of types or projects that depend on this one. High Ca means change here is expensive; demand stability.
- **Efferent coupling (Ce):** count of types or projects this one depends on. High Ce means fragility; the class has too many reasons to break.
- **Instability (I = Ce / (Ca + Ce)):** near 0 means stable and hard to change safely; near 1 means dependent and volatile. Flag stable types with large public surfaces and unstable types that many others depend on.
- Practical .NET checks: review the solution dependency graph, run `dotnet list <project> reference`, count constructor parameters, and count `using` directives per file. Tools such as NDepend and Visual Studio's code metrics report Ca/Ce directly.
- In a diff: did the change add a new project reference, a new injected service, or a new `using` on another module's namespace? Each one is a coupling increase that needs justification.

## Cohesion Types: What to Look For

Rank the unit under review against this scale, from worst to best. Recommend moving up the scale.

| Rank | Type | Signal in code |
|------|------|----------------|
| Worst | Coincidental | Grab-bag classes: `Utils`, `Helpers`, `Common`; contents share nothing but a file |
| Bad | Logical | Things grouped because they are the same kind of thing (`AllValidators`, one giant `Repositories` folder spanning domains) rather than because they change together |
| Bad | Temporal | Grouped only because they run at the same time (`StartupTasks` doing logging setup, cache warmup, and migrations) |
| Weak | Procedural | Steps grouped because they execute in order, even though they act on unrelated data |
| Better | Communicational | Members grouped because they operate on the same data |
| Better | Sequential | Output of one step feeds the next, forming one pipeline |
| Best | Functional | Every member contributes to one well-defined task; the class name says exactly what it does |

Review test: can you name the class or module without "And", "Manager", "Helper", or "Misc"? If not, cohesion is low. A cohesive module keeps together the code that changes together.

## Information Hiding

Parnas's rule: decompose by what is likely to change, and hide each design decision inside one module.

- **Encapsulation:** no public setters that let callers break invariants; expose behavior, not raw state. Fields private, mutation through methods that enforce rules.
- **Module boundaries:** a module's database schema, EF entities, and internal services are secrets. Nothing outside the module may reference them.
- **Public vs. internal APIs:** default every type to `internal`; make types `public` only when they belong to the module's deliberate contract. In a diff, treat any change from `internal` to `public` as a design decision that needs review, not a convenience fix. Treat `InternalsVisibleTo` outside of test projects as a smell.
- The public API is the set of assumptions you allow others to make. Everything hidden is a decision you can still reverse cheaply.

## Separation of Concerns

- One unit, one concern, one reason to change. UI, business rules, and data access do not belong in the same class.
- Watch for: controllers containing SQL or business rules, domain entities doing serialization or logging, one class mixing "what to do" with "how to persist it".
- Cross-cutting concerns (logging, validation, caching) belong in dedicated components applied around the core logic, not scattered through it.
- Separation of concerns is what makes low coupling and high cohesion achievable: separate the concerns first, and the cohesive groupings and thin interfaces follow.

## Checklist

Coupling:

- [ ] No new dependency (project reference, injected service, `using` on another module) without justification.
- [ ] No reference to another module's internal types, entities, or database tables.
- [ ] No boolean/enum control flags added to public method signatures.
- [ ] No whole entities passed where a few values suffice.
- [ ] No hidden call-order requirements; sequences are enforced by the API shape.
- [ ] Constructor parameter count is reasonable (many dependencies means split the class).
- [ ] High-afferent (widely used) types were not changed casually; their surface stays stable.

Cohesion:

- [ ] No additions to `Utils`, `Helpers`, `Common`, or `Shared` grab-bags.
- [ ] New code lives with the code it changes with, not with code that merely looks similar.
- [ ] Each new class has a single, nameable purpose; the name contains no "And" or "Manager".

Information hiding and separation of concerns:

- [ ] New types default to `internal`; every new `public` type is a deliberate contract decision.
- [ ] No public setters that bypass invariants; state changes go through behavior.
- [ ] No layer mixing: no data access in controllers, no presentation logic in the domain.
- [ ] Cross-cutting concerns are applied around the logic, not woven into it.

Verdict: state which coupling and cohesion types the diff introduces or removes, then recommend the specific fix from the tables above. Report findings before proposing rewrites.

## Related Skills

- **module-boundary-design**: use when the question is where module lines should be drawn, not how coupled the code is.
- **inter-module-communication**: use when modules need each other's data or behavior and you must choose the mechanism and contract.
- **fitness-functions**: use to turn violations found here into automated rules (ArchUnit.NET, NsDepCop, CI) so they cannot recur.
