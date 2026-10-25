# Exercise: Split or Merge?

**Module:** Modularity vs. Granularity (2-day module 9, 1-day module 7)
**Format:** teams of 3–5
**Time:** 2-day: 20 min + 10 min debrief. 1-day: 12 min discussion + 5 min debrief (decisions 1 and 2 only).

## Goal

Make service granularity decisions by naming the integrators and disintegrators that drive them, instead of going with gut feel.

## Situation

TicketTide shipped a first version as a modular monolith. Six months in, the team is debating these changes:

1. **Extract Inventory.** Seat holds and availability are hammered during on-sales, while the rest of the system is mostly idle. A slow seat-hold query recently made the venue dashboard slow too.
2. **Split Ordering and Payments into two services.** Payments code changes rarely, but a security review wants it isolated from everything else. An order and its payment are currently saved in one database transaction.
3. **Merge Notifications back into Ordering.** A previous team made Notifications a separate service. Now every new email requires changes in both services, and there's a shared `TemplateModels` library that changes weekly.
4. **Split Ticketing into "Issuing" and "Scanning".** Scanning runs mostly on devices at the door and has to work offline. Issuing creates QR codes when orders are paid. Both read and write the same ticket records.

## Steps

For each decision (2-day: all four; 1-day: 1 and 2):

1. List every **disintegrator** that applies: scope and function, code volatility, scalability and throughput, fault tolerance, security, extensibility.
2. List every **integrator** that applies: database transactions, workflow and choreography, shared code, data relationships.
3. Decide: **split**, **merge**, or **leave it**. One sentence on which force won and why.
4. Name the cost you're accepting.

## Debrief questions

- Which decision had the strongest forces pulling in *both* directions?
- Did any team avoid a split by solving the problem another way, such as a separate read model or a bulkhead inside the monolith?
- Which decisions need input from the business, not just the team?
- How would you record these decisions so the next team doesn't undo them?
