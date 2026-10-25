# Exercise: Find TicketTide's Bounded Contexts

**Module:** SOA, DDD, and Microservices (2-day module 8)
**Format:** teams of 3–5, whiteboard or sticky notes
**Time:** 15 min + 10 min debrief

## Goal

Find boundaries where a word means something different, and use them to propose bounded contexts and a context map.

## Materials

- [TicketTide brief](./tickettide/brief.md)

## Steps

1. **Collect the nouns (3 min).** List the important terms in the brief: *ticket, event, seat, order, venue, fan, scan, refund, settlement, ...*
2. **Find words with two meanings (5 min).** For each term, ask what it means to a fan, a venue manager, door staff, and finance. Circle every term whose meaning changes. For example, does a "ticket" mean the same thing at checkout as it does at the door?
3. **Draw the contexts (5 min).** Group terms into 4–6 bounded contexts. Each context gets a name and owns its own meaning of a term.
4. **Map the relationships (2 min).** Draw arrows between contexts and label who is upstream. Mark where you'd put an **anti-corruption layer**, for example around the payment provider or the legacy system.

## Debrief questions

- Which term changed meaning the most across contexts?
- Did any team end up with "entity services" like `TicketService` and `VenueService`? How is that different from a bounded context?
- Where does the legacy system fit on your map? What does the anti-corruption layer translate?
- Would each context become a microservice? Hold that thought for the next module on granularity.
