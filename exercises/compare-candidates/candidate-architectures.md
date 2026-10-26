# TicketTide: Two Candidate Architectures

Both candidates meet the functional requirements in the [brief](../tickettide/brief.md). They differ in how they handle the architecture characteristics.

## Candidate A: Modular monolith with an on-sale queue

- One ASP.NET Core application, deployed as a single unit, split into modules: **Events**, **Inventory**, **Orders**, **Ticketing**, **Reporting**.
- Modules talk through in-process interfaces and a small in-process message bus. Each module owns its own SQL Server schema in one database.
- For big on-sales, a **virtual waiting room** sits in front of checkout. Fans join a queue and are let in at a rate the system can handle.
- The app runs on 3 instances behind a load balancer. Before announced on-sales, the team scales out to 10 instances.
- Door scanners download the event's ticket list before doors open and scan offline, syncing scans when they can.
- Reporting reads from a read replica.

## Candidate B: Event-driven microservices

- Six services: **Catalog**, **Inventory**, **Checkout**, **Payments**, **Ticketing**, **Reporting**, each with its own database and deployed independently.
- Services publish events (`SeatsHeld`, `OrderPaid`, `TicketIssued`, `TicketScanned`) to a message broker.
- **Inventory** and **Checkout** autoscale on queue depth and CPU, from 2 to 40 instances, without manual steps.
- The **Ticketing** service has an edge API that scanners call, with an offline mode on the device.
- **Reporting** builds its own data store from events (no reach-in queries).
- Requires a container platform, a broker, distributed tracing, and on-call coverage for every service.

## Rough numbers

| | Candidate A | Candidate B |
| --- | --- | --- |
| Monthly hosting estimate | ~$4,000 | ~$11,000 |
| Time to first release | ~5 months | ~9 months |
| Deployable units | 1 | 6 (plus the broker) |
| New skills the team needs | Waiting room, offline sync | Containers, broker, tracing, sagas, offline sync |
