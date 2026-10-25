# Time Travel Saga (synchronous, eventually consistent, choreographed)

| Communication | Consistency | Coordination  |
| ------------- | ----------- | ------------- |
| Synchronous   | Eventual    | Choreographed |

There is **no orchestrator**. Each module commits its own work, then synchronously calls the next one,
like pipes and filters: Checkout calls Inventory, Inventory calls Payments, and Payments calls Ticketing.
The replies travel back up the chain to the fan.

When Ticketing fails, **nothing is rolled back**. The seats are held and the card is charged; those
transactions have already committed. Payments owns the hand-off to Ticketing, so Payments **retries**.
When Ticketing recovers, the data catches up and the order is confirmed.

This is what eventual consistency means here. For a while the modules disagree: the card is charged, but
there are no tickets yet. Retrying a technical failure brings them back in line. Business rejections, like
a declined card, are a different thing. Retrying will not change the answer, so they end the purchase.

Animated diagram: [diagrams/sagas/time-travel-saga](../../diagrams/sagas/time-travel-saga/index.html)

## The TicketTide workflow

A fan buys tickets for an event:

1. **Inventory** holds the seats.
2. **Payments** charges the card (through a fake payment provider).
3. **Ticketing** issues the tickets.
4. **Checkout** emails the fan (through Papercut, a fake SMTP server).

## How this sample is built

```text
TicketTide.AppHost          Aspire: PostgreSQL (4 databases), Papercut, and the API
TicketTide.Api              ASP.NET Core host (FastEndpoints + Mediator). Wires the modules together.
TicketTide.Checkout         Orders, the start of the chain, fan emails              -> checkoutdb
TicketTide.Inventory        Events and seat holds, then calls Payments              -> inventorydb
TicketTide.Payments         Charges, then calls Ticketing and owns its retries      -> paymentsdb
TicketTide.Ticketing        Ticket issuing (with failure injection)                 -> ticketingdb
TicketTide.*.Contracts      The commands each module accepts. Public messages only.
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

It is a **modular monolith**: one process, but each module owns its own database, and modules only
know each other's `Contracts`. The project references follow the chain:

```text
Checkout  -> Inventory.Contracts                         (HoldSeats)
Inventory -> Payments.Contracts                          (ChargeCard)
Payments  -> Ticketing.Contracts, Checkout.Contracts     (IssueTickets, TicketsIssued)
Ticketing -> nothing
```

Checkout does not know that Payments or Ticketing exist. It sends one command, `HoldSeats`, and gets
one reply. Each command carries what the later steps need, such as the payment token. Each reply carries
the outcome (`Done`, `Pending`, or `Failed`), the ticket codes, the failure reason, and the steps each module
took, so Checkout can record the order's history.

In choreography, each service knows who to tell next. Usually that is the next link in the chain.
Payments also knows Checkout, because when it finishes a hand-off late, the fan's request is long gone
and there is no chain of replies to travel back up. So Payments tells Checkout directly (`TicketsIssued`).

The saga is spread over three handlers, one per module:

- [`CheckoutService`](src/TicketTide.Checkout/Saga/CheckoutService.cs) saves the order, starts the chain,
  and records the reply.
- [`HoldSeatsHandler`](src/TicketTide.Inventory/Handlers/HoldSeatsHandler.cs) holds the seats, calls Payments,
  and gives the seats back if the card is not charged.
- [`ChargeCardHandler`](src/TicketTide.Payments/Handlers/ChargeCardHandler.cs) charges the card, calls Ticketing,
  and retries it. [`TicketHandoffRetrier`](src/TicketTide.Payments/Handoffs/TicketHandoffRetrier.cs) keeps
  retrying in the background, and
  [`TicketsIssuedHandler`](src/TicketTide.Checkout/Saga/TicketsIssuedHandler.cs) confirms the order when it
  gets through.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/time-travel-saga
dotnet run --project src/TicketTide.AppHost
```

Open the Aspire dashboard link printed in the console. When `api` is healthy:

- The API is at `http://localhost:5240`.
- The home page, [`http://localhost:5240/`](http://localhost:5240/), has a button for each walkthrough below and a form
  for your own orders. It shows the order's history and what each module recorded.
- Papercut's inbox is the `papercut` resource's `http` endpoint in the dashboard.
- [`TicketTide.Api.http`](src/TicketTide.Api/TicketTide.Api.http) has every request below, ready to run in
  Visual Studio, VS Code (REST Client), or Rider.

The databases are not persistent: every run starts with fresh data.

## Walkthrough 1: the happy path

See what is on sale:

```sh
curl http://localhost:5240/inventory/events
```

Buy two tickets for The Midnight Tide:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"fan@example.com","paymentToken":"tok_visa"}'
```

The request travels down the chain, the replies travel back up, and the fan gets `201 Created`:

```json
{
  "orderId": "0199...",
  "status": "Confirmed",
  "eventName": "The Midnight Tide",
  "amount": 90.00,
  "ticketCodes": ["TT-0199ABCD-01", "TT-0199ABCD-02"],
  "history": [
    "Order placed.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing issued 2 ticket(s).",
    "Tickets confirmed."
  ]
}
```

Check it:

- `GET /inventory/events` shows 2 fewer seats for `midnight-tide`.
- `GET /inventory/holds/{orderId}` is `Held`, `GET /payments/{orderId}` is `Charged`, `GET /tickets/{orderId}` is `Issued`.
- Papercut has a "Your tickets for The Midnight Tide" email to `fan@example.com`.
- In the dashboard's **Traces** view, the `POST /orders` trace shows each module's database calls, one after another.

## Walkthrough 2: Ticketing fails, so Payments retries

`simulateTicketingFailures` makes Ticketing fail that many attempts for this order. Ask for 2 failures:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"patient-fan@example.com","paymentToken":"tok_visa","simulateTicketingFailures":2}'
```

Ticketing fails. Nothing is rolled back. Payments owns this hand-off, so it retries, up to 5 times,
200 ms apart, while the fan waits. On the third attempt Ticketing recovers, the data catches up, and the
replies travel back up the chain. The fan gets `201 Created`:

```json
{
  "status": "Confirmed",
  "history": [
    "Order placed.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing failed (attempt 1 of 5): Ticketing is unavailable (simulated failure 1 of 2). Nothing is rolled back.",
    "Ticketing failed (attempt 2 of 5): Ticketing is unavailable (simulated failure 2 of 2). Nothing is rolled back.",
    "Ticketing issued 2 ticket(s) on attempt 3.",
    "Tickets confirmed."
  ]
}
```

Check each module:

- `GET /inventory/holds/{orderId}` is still `Held`. Inventory never had to undo anything.
- `GET /payments/{orderId}` is still `Charged`. There is no refund.
- `GET /tickets/{orderId}` is `Issued` with `attempts: 3`.
- Papercut has a "Your tickets for The Midnight Tide" email to `patient-fan@example.com`.

### A longer outage: the fan gets "Pending"

Now ask for 8 failures. That is more than the 5 attempts Payments makes while the fan waits:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"very-patient-fan@example.com","paymentToken":"tok_visa","simulateTicketingFailures":8}'
```

Payments stops making the fan wait. It records the outstanding hand-off in its own database (the
`TicketHandoffs` table in `paymentsdb`) and replies `Pending`. The reply travels up the chain, and the
fan gets `202 Accepted`:

```json
{
  "status": "Pending",
  "ticketCodes": [],
  "history": [
    "Order placed.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing failed (attempt 1 of 5): ...",
    "...",
    "Ticketing failed (attempt 5 of 5): Ticketing is unavailable (simulated failure 5 of 8). Nothing is rolled back.",
    "Ticketing is still down. Payments will keep retrying in the background.",
    "Tickets pending. Checkout will confirm the order when Payments reports the tickets issued."
  ]
}
```

Right now the modules disagree. The seats are held and the card is charged, but there are no tickets.
Payments still owns the hand-off: a background service in the Payments module retries Ticketing once a second.
Run `GET /orders/{orderId}` a few times. After a few seconds Ticketing recovers (on attempt 9), Payments
tells Checkout, and the order ends like this:

```json
{
  "status": "Confirmed",
  "history": [
    "...",
    "Tickets pending. Checkout will confirm the order when Payments reports the tickets issued.",
    "Payments retried in the background, and Ticketing issued 2 ticket(s).",
    "Tickets confirmed."
  ]
}
```

`GET /tickets/{orderId}` shows `attempts: 9`, and the confirmation email arrives in Papercut.

## More failure paths

- **Card declined:** `"paymentToken":"tok_declined"`. A declined card is a business answer, not a technical
  failure, so nobody retries it. Inventory handles its own error: it releases the seats it held, then replies
  `Failed`. Ticketing is never called. The fan gets `422` and a "could not be completed" email.
- **Sold out:** `"eventId":"back-room-sessions","quantity":5`. Inventory cannot hold the seats, so the chain stops
  at the first link. The card is never charged.
- **Bad request:** `"quantity":0` or an invalid email returns `400` with validation errors.

## Run the tests

```sh
cd samples/time-travel-saga
dotnet test
```

The tests start the whole AppHost (PostgreSQL and Papercut containers included), so Docker must be running.
[`TimeTravelSagaTests`](tests/TicketTide.EndToEndTests/TimeTravelSagaTests.cs) covers the happy path, a Ticketing
failure that Payments retries while the fan waits, a longer outage that ends `Pending` and catches up in the
background, a declined card, a sold-out event, and validation.

## Things to notice

- **Simple chains get high throughput.** No orchestrator sits in the middle of every call. Each module does its
  part and passes the work on.
- **No rollback, just retry.** A technical failure is retried until it succeeds, so there are no compensating
  transactions for Ticketing failures. Only business rejections end a purchase.
- **Each module handles its own errors.** Inventory releases its own seats when the card is declined. Payments
  retries its own hand-off to Ticketing. Nobody has the whole picture, so as the workflow grows, it quickly
  becomes hard to follow. To see what happened to an order, you read the history every module reported.
- **An error is not a "no".** If Payments throws instead of answering, the card may already be charged. So
  Inventory keeps the seats and replies `Pending`, and the order's history says it needs manual attention. Only a
  definite answer, like a declined card, makes Inventory give the seats back and tell the fan they were not
  charged. In the same way, once Payments has charged the card it never replies `Failed`.
- **The fan still waits.** The calls are synchronous, so the response time is the sum of every step, plus the
  inline retries. That is why Payments gives up waiting after a few attempts and returns `Pending`.
- **Steps must be idempotent.** Ticketing returns the same tickets if it is asked twice. Payments never charges an
  order twice: it also sends the provider an idempotency key, so a retry after a failed save is not a second
  charge. Checkout ignores a second `TicketsIssued`. Retrying is only safe because of this.
- **Not crash-safe.** The inline retries live in memory. If the process dies before Payments records the hand-off,
  the order stays `Placed`. A production system would record the hand-off before the first attempt (an outbox),
  or use a durable queue.

Compare with the [Phone Tag Saga](../phone-tag-saga/), which also has no orchestrator and synchronous calls, but is
atomic: a Ticketing failure is undone instead of retried. And with the [Fairy Tale Saga](../fairy-tale-saga/),
which retries Ticketing the same way but puts an orchestrator in charge of every step.
