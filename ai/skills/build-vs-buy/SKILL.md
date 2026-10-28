---
name: build-vs-buy
description: Use when deciding whether to build a component, buy a product or SaaS, or wrap a vendor behind your own interface; when evaluating a third-party dependency's total cost of ownership, lock-in, and exit strategy; or when asking whether AI-assisted coding changes the answer.
version: 1.0.0
author: ardalis
---

# Build vs Buy

## Overview

Code is getting cheaper to write. Owning it is not. Build cost is the smallest part of total cost of ownership; maintenance, security fixes, compliance, and operations dominate. Build what differentiates you, buy what does not, and put your own interface in front of anything you might want to change later.

## When to Use

- Choosing between a vendor, SaaS, or library and writing it yourself.
- Adding a significant third-party dependency.
- Someone says "we could just build that with an AI assistant in a week."

**When NOT to use:**

- Comparing whole-system architectures (use trade-off-analysis).
- Choosing among small utility packages with trivial swap cost.

## Step 1: Classify the Domain

| Domain type | Definition | Default | TicketTide example |
|-------------|-----------|---------|-----------|
| Core | Why customers pick you | **Build** | Seat map editor, on-sale experience |
| Supporting | Specific to you, not a differentiator | Build simply, or buy and customize | Virtual waiting room, per-venue feature flags |
| Generic | Every business needs it the same way | **Buy** | Payments, email delivery |

Trivial generic pieces (QR code generation) are a library call, not a project.

## Step 2: Three Options, Not Two

| Option | Choose when |
|--------|-------------|
| Buy | Generic, regulated, or operationally heavy; mature vendor |
| Build | Core domain; nothing fits; you can staff it for years |
| Wrap-and-buy | Buy now, keep the option to switch |

Wrap-and-buy: an interface you own, in your domain terms (`IPaymentGateway`, `IEmailSender`), with the vendor SDK confined to one adapter. Callers depend on the interface, never the SDK.

## Step 3: Total Cost of Ownership

Estimate over 2-3 years, both sides:

| Cost | Build | Buy |
|------|-------|-----|
| Initial | Development, testing | Integration, onboarding |
| Recurring | Maintenance, upgrades, hosting | License, usage fees, price increases |
| Risk | Security fixes, bugs, key-person loss | Vendor outage, deprecation, acquisition |
| Compliance | You carry it (PCI, GDPR) | Shared with or transferred to vendor |
| Operations | On-call, monitoring, deliverability | Support tier, SLAs |
| Opportunity | Engineers not working on core | Little |

Ask: **who maintains this in two years, and who handles the security fixes?**

## Step 4: What Stays "Buy" However Cheap Code Gets

- **Compliance burden** (PCI for card data).
- **Reputation and network effects** (email deliverability, sender reputation).
- **Heavy operational load** (24/7 global infrastructure).
- **Specialist expertise** (fraud detection, tax rules).

AI assistants lower build cost for small, well-understood, low-risk components. They do not lower ownership cost.

## Step 5: Lock-in and Exit Strategy

| Lock-in source | Mitigation |
|---------------|-----------|
| Vendor SDK types in your domain | Own the interface; adapter at the edge |
| Proprietary data formats | Confirm export; keep your own copy of key data |
| Vendor-specific features | Use them deliberately and record it in the ADR |
| Contract terms | Know notice periods and data return terms |

Write the exit plan before signing: what triggers leaving, how long it takes, what it costs. If you cannot describe the exit, you are more locked in than you think.

## Step 6: Plan for Failure

Every bought service is a remote dependency that will fail. Apply dependency-failure-resilience: timeouts, retries, circuit breakers, and an honest user-facing fallback.

## Step 7: Record the Decision

Route to adr-writing. Include domain classification, options considered, TCO estimate, lock-in accepted, exit trigger, and a review date.

## Checklist

- [ ] Domain classified as core, supporting, or generic
- [ ] Wrap-and-buy considered
- [ ] TCO over 2-3 years, including maintenance and security
- [ ] Owner named for the next two years
- [ ] Lock-in sources listed with mitigations
- [ ] Exit trigger and plan written
- [ ] Failure handling designed
- [ ] ADR recorded

## Common Mistakes

- Comparing build effort to license price and ignoring operations.
- Building generic capability because it is fun or "only a week."
- Leaking vendor types throughout the codebase.
- Buying a core-domain capability and giving away your differentiation.

## Related Skills

adr-writing, trade-off-analysis, dependency-failure-resilience, module-boundary-design, coupling-cohesion-analysis
