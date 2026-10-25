# Discussion: Handling Dependency Failures

**Module:** Distributed Systems Fundamentals (2-day module 7, 1-day module 5)
**Format:** small groups, then whole room
**Time:** 2-day: 12 min in groups + 8 min debrief. 1-day: 8 + 5.

## Goal

Decide what TicketTide should do when something it depends on is down, for reads and for writes, and name what each choice costs.

## Scenarios

Each group takes one scenario (1-day: scenarios 1 and 2 only).

1. **The seat map service is down** during a busy evening. Fans can still reach the site.
2. **The payment provider times out** for 3 minutes during a big on-sale.
3. **The reporting database is unreachable**, and a venue manager opens the sales dashboard before a show.
4. **The email provider is rejecting messages**, so ticket confirmations can't be sent.
5. **A door scanner loses its connection** with 2,000 fans still in line.

## For your scenario, answer

1. Is this a **read** or a **write** problem, or both?
2. What does the user see if you do nothing?
3. What will you do instead? Consider:
   - **On read:** serve from a cache. How stale can it be? How will the user know?
   - **On write:** accept into a queue and process later. What happens if processing fails later? Is the handler idempotent?
   - **Fail fast** with a clear message.
4. What do you tell the user, honestly? "Your order is confirmed" and "We received your order" are different promises.
5. Which architecture characteristic are you trading away?

## Debrief questions

- Which scenarios could use a cache, and which absolutely couldn't?
- For queued writes, what makes a retry safe? What breaks if a message is processed twice?
- Where did the answer depend on the business rather than on technology? Who would you ask?
- Did the CAP/PACELC choice differ between scenarios in the same system?
