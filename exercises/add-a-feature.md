# Exercise: Add the Same Feature to Two Samples

**Module:** Monolithic Architecture Styles (2-day module 5, 1-day module 4)
**Format:** pairs; half the room uses one pair of samples, half the other
**Time:** 2-day: 30 min + 10 min debrief. 1-day: 20 min + 5 min debrief.

> **Status:** this exercise needs the Clean Architecture and Vertical Slice samples in [`samples/`](../samples/), which haven't been built yet. The feature spec and debrief below are ready. Each sample should include an `Order` aggregate and a "place order" use case that this feature extends.

## Goal

Feel the difference between styles by making the same change in each, then compare how many places you touched and why.

## The feature: ticket transfer

A fan can transfer a paid ticket to a friend by email.

- `POST /orders/{orderId}/tickets/{ticketId}/transfer` with `{ "toEmail": "friend@example.com" }`
- Only **paid** tickets can be transferred, and a ticket can be transferred **at most twice**.
- The new holder gets an email. The original holder's ticket stops working.
- Return `404` for unknown orders or tickets and `400` when a rule is broken.

## Steps

1. **Implement it in the first sample (12 min).** Don't refactor anything you don't have to.
2. **Implement it in the second sample (12 min).**
3. **Count (3 min).** For each sample, record the number of files touched, files added, and projects or folders touched. Note where each business rule ended up.
4. **Compare (3 min).** Which style made the change easier? Which made the rule easier to find later?

1-day: implement it in one sample, then pair up with a team that used the other sample and compare.

## Debrief questions

- Where did the "at most twice" rule live in each sample? Is that where you'd look for it in six months?
- Which sample made you touch more layers? Was that friction useful or just ceremony?
- If transfer had to become its own service later, which sample would make that easier?
- How does an AI agent do on each? Did one style give it better context about where code belongs?
