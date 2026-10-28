# AI Agents and Skills

Skills, an agent, and a starter `AGENTS.md` that put the workshop's ideas to work in your own repositories. They work with Claude Code, GitHub Copilot, Codex, and other agents that read `AGENTS.md` and `SKILL.md` files.

## Use them in your repository

1. Copy [`AGENTS.md`](./AGENTS.md) to the root of your repository and edit it to match your decisions. If you use Claude Code, also add a `CLAUDE.md` containing the line `@AGENTS.md`.
2. Copy the skills you want from [`skills/`](./skills/) into `.agents/skills/` (and into `.claude/skills/` for Claude Code).
3. Copy the agent from [`agents/`](./agents/) into `.claude/agents/`.

## [`AGENTS.md`](./AGENTS.md)

Three lines: keep ADRs in `docs/adr/` and respect them, fix the code (not the rule) when a fitness function fails, and load the relevant skill before architecture work. It's the guardrail from the [write an ADR](../exercises/write-an-adr.md) exercise, plus the lesson from [write fitness functions](../exercises/fitness-functions/).

## Skills

| Skill | Use it when |
| --- | --- |
| [`trade-off-analysis`](./skills/trade-off-analysis/) | Identifying architecture characteristics, setting measurable targets, and scoring candidate architectures |
| [`architecture-style-selection`](./skills/architecture-style-selection/) | Choosing or validating a style: layered, Clean Architecture, vertical slice, modular monolith, service-based, event-driven, or microservices |
| [`adr-writing`](./skills/adr-writing/) | Writing, superseding, or indexing an ADR. Includes a [template](./skills/adr-writing/template.md) and [examples](./skills/adr-writing/examples/). |
| [`build-vs-buy`](./skills/build-vs-buy/) | Deciding whether to build a component, buy it, or wrap a vendor behind your own interface |
| [`fitness-functions`](./skills/fitness-functions/) | Writing automated checks for architecture characteristics, or keeping a boundary violation from coming back |
| [`coupling-cohesion-analysis`](./skills/coupling-cohesion-analysis/) | Reviewing a diff or codebase for design quality, or diagnosing code that's hard to change or test |
| [`module-boundary-design`](./skills/module-boundary-design/) | Splitting a system into modules, deciding which module owns a feature, or reviewing a shared kernel |
| [`modular-solution-layout`](./skills/modular-solution-layout/) | Scaffolding a modular .NET solution, adding a module, and organizing code inside it (Clean Architecture vs. Vertical Slice) |
| [`inter-module-communication`](./skills/inter-module-communication/) | One module needs another's data or behavior (contracts, sync calls, or events), including cross-module reports |
| [`dependency-failure-resilience`](./skills/dependency-failure-resilience/) | Designing for a slow or unavailable dependency: timeouts, retries, circuit breakers, fallbacks, idempotency |
| [`modular-monolith-testing`](./skills/modular-monolith-testing/) | Organizing tests and testing module contracts and events |

## Agents

| Agent | What it does |
| --- | --- |
| [`software-architect`](./agents/software-architect.md) | Reviews code, designs, and plans with trade-offs first. It routes each task to the skills above, records decisions as ADRs, proposes a fitness function for anything that can drift, and states the trade-off behind each recommendation. It expects the skills in `.agents/skills/`. |
