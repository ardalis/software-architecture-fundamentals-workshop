# Exercise: Compare Two Candidate Solutions

**Module:** Trade-off Analysis (2-day module 4, 1-day module 3, part 2)
**Format:** same teams as the kata
**Time:** 20 min of team work + 10 min debrief (1-day: 10 + 5)

## Goal

Choose between two reasonable architectures by weighing them against the characteristics your team picked, and write down why.

## Materials

- Your top three characteristics from the [architecture kata](../architecture-kata/)
- [TicketTide candidate architectures](./candidate-architectures.md), in this folder

## Steps

1. **Weight your characteristics (3 min).** Give your top three weights of 3, 2, and 1. Add *cost* and *simplicity* with weight 1 if they aren't already in your list.
2. **Rate each candidate (8 min).** Score each candidate from 1 to 5 stars on each characteristic. Write one sentence justifying every score.
3. **Total it up (2 min).** Multiply each score by its weight and add them up.
4. **Sanity-check the result (4 min).** Does the winner *feel* right? If not, which weight or score is wrong? Change it only if you can explain why.
5. **Record the decision (3 min).** Write a short decision statement:
   > *We chose ___ because ___. We accept ___ as a consequence.*

## Scoring table

| Characteristic | Weight | A score | A × weight | B score | B × weight | Justification |
| --- | :-: | :-: | :-: | :-: | :-: | --- |
| | 3 | | | | | |
| | 2 | | | | | |
| | 1 | | | | | |
| Cost | 1 | | | | | |
| Simplicity | 1 | | | | | |
| **Total** | | | | | | |

Keep this table and your decision statement. You'll turn them into an ADR on Day 2 (1-day: in the afternoon).

## Debrief questions

- Did any team's total pick the "wrong" winner by their own judgment? What did they change?
- Which single score, if changed by one star, would flip your decision?
- Is there a hybrid, such as Candidate A now with Inventory extracted later? What would have to be true to justify it?
- What did each team accept as a consequence of its choice?
