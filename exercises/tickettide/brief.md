# TicketTide: Case Study Brief

TicketTide is the fictional system used across the workshop exercises. The kata, trade-off analysis, bounded contexts, granularity, and ADR exercises all build on it, so decisions made early in the workshop carry forward.

## The business

TicketTide sells tickets for about 120 independent venues across three states: music clubs, theaters, comedy rooms, and a few mid-size arenas (up to 18,000 seats). Venues pay TicketTide a per-ticket fee. Today they use a 12-year-old system that a contractor built and nobody wants to touch.

The company has 40 employees, including 9 developers in two teams. The developers know .NET and SQL Server well and have little experience operating distributed systems.

## Users

- **Fans** buy tickets on the web and in the mobile app.
- **Venue managers** create events, set seat maps and prices, and see sales reports.
- **Door staff** scan tickets at the entrance with phones or handheld scanners.
- **TicketTide support** handles refunds, transfers, and fraud reviews.

## What the system must do

1. Venue managers publish events with a seat map (reserved seating) or a capacity (general admission).
2. Fans browse events, pick seats, and check out. Seats are held for 8 minutes during checkout.
3. Payments go through an external payment provider.
4. Fans receive tickets (QR codes) by email and in the app, and can transfer them to friends.
5. Door staff scan tickets at entry. A ticket can only be scanned once.
6. Venues see near-real-time sales dashboards and get a settlement report every week.
7. Support can refund, reissue, or cancel tickets.

## What the stakeholders said

> **CEO:** "When a big act goes on sale, we cannot crash. Last spring we went down for 40 minutes on a sold-out show and it was on the local news."

> **Head of venue partnerships:** "Venues leave us when their reports are wrong. A venue manager needs to trust the numbers."

> **Operations lead:** "Most venues have terrible cell coverage near the doors. Scanning can't stop working just because the network drops."

> **CFO:** "We're a small company. I'd rather not triple our hosting bill. Also, two of our largest venues want to sign by Q3, and they want their own branding on the checkout pages."

> **Lead developer:** "Every change to the old system takes weeks because everything touches everything. I'd like to be able to ship small changes safely."

## Numbers

- Typical traffic: ~50 orders per minute across all venues.
- Big on-sale events: up to 40,000 fans arrive in the first 5 minutes for a single arena show.
- About 6 big on-sale events per month.
- Door scanning: up to 18,000 scans in the 90 minutes before a show starts.
- Two developers are on call; there is no dedicated operations team.

## Constraints

- Card data never touches TicketTide servers (the payment provider handles it).
- Settlement reports must be exactly right; they drive what venues get paid.
- The new system must run alongside the old one during migration.
