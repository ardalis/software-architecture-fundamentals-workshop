# Discussion: Would You Build It Now?

**Module:** Third-Party Dependency Decisions (2-day module 14)
**Format:** whole room, quick show of hands, then discussion
**Time:** 10 min

## Goal

Test the claim that LLMs change the buy-vs.-build trade-off, using concrete TicketTide components.

## For each component, vote: buy, build, or wrap-and-buy

| Component | What TicketTide needs |
| --- | --- |
| Payment processing | Card payments, refunds, chargebacks, PCI compliance |
| QR code generation | Turn a ticket ID into a scannable image |
| Virtual waiting room | Queue fans during on-sales and let them in at a steady rate |
| Email delivery | Send confirmation and transfer emails reliably |
| Seat map editor | Let venue managers draw and edit seat maps in the browser |
| Feature flags | Turn features on per venue |

After each vote, ask one person from each side to give their strongest reason.

## Questions to drive the discussion

- Which component would you have bought two years ago but might build now with an AI assistant? What changed?
- Which ones stay "buy" no matter how cheap code gets? Why? (Consider compliance, deliverability, and operational load.)
- If you build, who maintains it in two years? Who handles the security fixes?
- Where would you put your own interface in front of a vendor so you could change your mind later?
