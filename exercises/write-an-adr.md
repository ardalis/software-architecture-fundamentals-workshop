# Exercise: Write an ADR

**Module:** Architecture Decision Records (2-day module 10, 1-day module 8)
**Format:** same teams as the kata
**Time:** 2-day: 20 min + 5 min debrief. 1-day: 12 min + 3 min debrief.

## Goal

Turn a decision you already made in the workshop into an ADR that a new team member, or an AI agent, could act on.

## Materials

- Your scoring table and decision statement from [Compare Two Candidate Solutions](./compare-candidates.md), or one decision from [Split or Merge?](./split-or-merge.md)
- [ADR template](./templates/adr-template.md) (Michael Nygard's format)

## Steps

1. **Pick the decision (2 min).** Use the architecture choice from the trade-off exercise unless your team prefers a granularity decision (2-day only).
2. **Write the ADR (12 min).** Fill in the template. Keep it to one page.
   - **Context:** the forces, including the characteristics and numbers that mattered.
   - **Decision:** one or two sentences in active voice: "We will ...".
   - **Consequences:** the good *and* the bad. What becomes harder?
3. **Swap and review (4 min).** Give your ADR to another team. They answer two questions in writing:
   - Could I explain *why* this was decided without asking you?
   - What would make me want to supersede this ADR?
4. **Add a guardrail (2 min).** Write one sentence for `AGENTS.md` that would stop an AI agent from quietly undoing this decision.

1-day: skip the swap and review.

## Debrief questions

- What was hardest to write: context, decision, or consequences?
- What did the reviewing team ask that the ADR didn't answer?
- What would trigger superseding this ADR? Is that trigger something a fitness function could detect?
