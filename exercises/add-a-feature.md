# Exercise: Add the Same Feature to Two Samples

**Module:** Monolithic Architecture Styles (2-day module 5, 1-day module 4)
**Format:** pairs. 2-day: each pair changes both samples. 1-day: half the room changes one sample, half the other
**Time:** 2-day: 30 min + 10 min debrief. 1-day: 20 min + 5 min debrief.

## Goal

Feel the difference between styles by making the same change in each, then compare how many places you touched and why.

## Materials

- [Clean Architecture sample](../samples/clean-architecture/): Core, Infrastructure, and Web projects
- [Vertical Slice sample](../samples/vertical-slice/): one folder per feature

Both samples have the same API (`GET /events`, `POST /orders`, `GET /orders/{orderId}`, `POST /orders/{orderId}/refund`), the same behavior, and the same end-to-end tests. Read each sample's README first: its "Where things live" table shows where the existing rules are. Build and run the tests before you start, so you know they pass:

```sh
dotnet BuildTestFormat.cs clean-architecture
dotnet BuildTestFormat.cs vertical-slice
```

## The feature: ticket transfer

A fan can transfer a paid ticket to a friend by email.

- `POST /orders/{orderId}/tickets/{ticketId}/transfer` with `{ "toEmail": "friend@example.com" }`
- Only **paid** tickets can be transferred, and a ticket can be transferred **at most twice**.
- The new holder gets an email. The original holder's ticket stops working: the ticket gets a new code, and the old code no longer gets anyone in.
- Return `200` with the order, `404` for unknown orders or tickets, and `400` when a rule is broken.
- If you have time, add end-to-end tests for the happy path and each rule to `tests/TicketTide.EndToEndTests/OrdersApiTests.cs`. The database is created fresh on every run, so a new column needs no migration.

## Steps

1. **Implement it in the first sample (12 min).** Don't refactor anything you don't have to. You can hand the work to your AI agent: give it the feature section above as the prompt, then review where it put each rule before you accept the change.
2. **Implement it in the second sample (12 min).**
3. **Count (3 min).** For each sample, record the number of files touched, files added, and projects or folders touched. Note where each business rule ended up.
4. **Compare (3 min).** Which style made the change easier? Which made the rule easier to find later?

1-day: implement it in one sample, then pair up with a team that used the other sample and compare.

## Debrief questions

- Where did the "at most twice" rule live in each sample? Is that where you'd look for it in six months?
- Which sample made you touch more layers? Was that friction useful or just ceremony?
- If transfer had to become its own service later, which sample would make that easier?
- How does an AI agent do on each? Did one style give it better context about where code belongs?
