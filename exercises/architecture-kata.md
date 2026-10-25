# Exercise: Architecture Kata

**Module:** "-ilities" (2-day module 3, 1-day module 3, part 1)
**Format:** teams of 3–5
**Time:** 20 min of team work + 10 min debrief (1-day: 15 + 5, then go straight to [comparing candidates](./compare-candidates.md))

## Goal

Practice getting architecture characteristics out of a business description, choosing only the few that matter most, and making them measurable.

## Materials

- [TicketTide brief](./tickettide/brief.md)
- [Characteristics worksheet](./characteristics-worksheet.md)

## Steps

1. **Alone: read the brief (5 min).** Each person reads it alone and highlights anything that sounds like a quality requirement rather than a feature.
2. **Team: list the candidates (5 min).** As a team, fill in the "Evidence" column of the worksheet. Quote the brief. Mark each one *explicit* (someone asked for it) or *implicit* (nobody said it, but it would be a failure if it were missing).
3. **Team: pick the top three (5 min).** You can't have more than three. Expect to argue.
4. **Team: make them measurable (5 min).** Give each of your three a target you could test, such as "checkout p99 under 2 s with 40,000 users in 5 minutes".
5. **Team: name one you're giving up.** Record one characteristic a stakeholder cares about that you didn't prioritize, and why.

## Deliverable

A completed "top three" table and one "not prioritized" row, ready to share in under 2 minutes.

## Debrief questions

- Which characteristic did every team pick? Which ones split the room?
- What implicit characteristics did teams find that no stakeholder mentioned?
- Do the characteristics apply to the *whole* system, or only to parts of it, like checkout or door scanning? What does that suggest about architecture quanta?
- Which targets would be hardest to test before go-live?
