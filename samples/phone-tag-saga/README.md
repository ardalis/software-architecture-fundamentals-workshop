# Phone Tag Saga (synchronous, atomic, choreographed)

| Communication | Consistency | Coordination  |
| ------------- | ----------- | ------------- |
| Synchronous   | Atomic      | Choreographed |

There is **no orchestrator**. Each module knows the next step and calls it directly, then waits for the answer:
Checkout calls Inventory, Inventory calls Payments, and Payments calls Ticketing. The replies travel back up the
same chain. If a step fails, the failure travels back up the chain too, and each module **undoes its own step**
on the way: Payments refunds the card, then Inventory releases the seats, then Checkout tells the fan.
The fan waits for the whole chain. Either everything happens, or nothing does.

It is like a game of phone tag: each service passes the message on and waits to hear back.
Nothing sits in the middle, but every service now carries a piece of the workflow and its own undo logic.

Animated diagram: [diagrams/sagas/phone-tag-saga](../../diagrams/sagas/phone-tag-saga/index.html)

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
TicketTide.Checkout         Orders, the first link in the chain, fan emails   -> checkoutdb
TicketTide.Inventory        Seat holds, then calls Payments; releases seats   -> inventorydb
TicketTide.Payments         Charges, then calls Ticketing; refunds the card   -> paymentsdb
TicketTide.Ticketing        Ticket issuing (with failure injection)           -> ticketingdb
TicketTide.*.Contracts      The commands each module accepts. Public messages only.
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

It is a **modular monolith**: one process, but each module owns its own database, and modules only
know each other's `Contracts`.

The project references follow the chain. Each module references only the **next** module's contracts:

```text
Checkout  -> TicketTide.Inventory.Contracts
Inventory -> TicketTide.Payments.Contracts
Payments  -> TicketTide.Ticketing.Contracts
Ticketing -> (nothing: it is the end of the chain)
```

Compare this with the [Epic Saga](../epic-saga/), where Checkout references all three contracts and the other
modules reference nobody. Here Checkout cannot even see Payments or Ticketing.

Calls between modules are synchronous, in-process [Mediator](https://github.com/martinothamar/Mediator)
commands: `HoldSeats`, `ChargeCard`, and `IssueTickets`. There are no public compensation commands. Each module
keeps its undo step private, because nobody else is meant to call it:

- [`HoldSeatsHandler`](src/TicketTide.Inventory/Handlers/HoldSeatsHandler.cs) holds the seats, sends `ChargeCard`,
  and releases the seats if the reply is a failure.
- [`ChargeCardHandler`](src/TicketTide.Payments/Handlers/ChargeCardHandler.cs) charges the card, sends
  `IssueTickets`, and refunds the card if the reply is a failure.
- [`IssueTicketsHandler`](src/TicketTide.Ticketing/Handlers/IssueTicketsHandler.cs) issues the tickets. It has
  nothing to undo.
- [`CheckoutStep`](src/TicketTide.Checkout/Saga/CheckoutStep.cs) saves the order, sends `HoldSeats`, and records
  the answer.

With no orchestrator to remember the details, the commands carry what the rest of the chain needs. `HoldSeats`
includes the payment token (for Payments) and `simulateTicketingFailures` (for Ticketing). `ChargeCard` includes
the event and quantity (for Ticketing).

The replies carry the results back: the ticket codes, the failure reason, and a list of the steps each module
took. Checkout cannot see the middle of the chain, so it builds the order's history from that list.
`GET /orders/{id}` shows the order's status and that step-by-step history.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/phone-tag-saga
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

The request blocks until the whole chain has answered, then returns `201 Created`:

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

Checkout wrote only the first and last lines. The three in the middle came back up the chain in the replies.

Check it:

- `GET /inventory/events` shows 2 fewer seats for `midnight-tide`.
- `GET /inventory/holds/{orderId}` is `Held`, `GET /payments/{orderId}` is `Charged`, `GET /tickets/{orderId}` is `Issued`.
- Papercut has a "Your tickets for The Midnight Tide" email to `fan@example.com`.
- In the dashboard's **Traces** view, the `POST /orders` trace shows each module's database calls, one after
  another: Inventory's hold, Payments' charge, Ticketing's tickets. All inside one request.

## Walkthrough 2: Ticketing fails, and the failure travels back along the chain

`simulateTicketingFailures` makes Ticketing fail that many attempts for this order. An atomic saga does not
retry; one failure undoes the whole purchase.

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"unlucky-fan@example.com","paymentToken":"tok_visa","simulateTicketingFailures":1}'
```

The fan waits, then gets `422 Unprocessable Entity`:

```json
{
  "status": "Failed",
  "failureReason": "Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 1 of 1).",
  "history": [
    "Order placed.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 1 of 1).",
    "Payments refunded the card.",
    "Inventory released the seats.",
    "Purchase failed."
  ]
}
```

Here is what happened, step by step:

1. Ticketing failed and answered Payments with "Failed".
2. Payments refunded the card, then answered Inventory with "Refunded, Failed".
3. Inventory released the seats, then answered Checkout with "Released, Failed".
4. Checkout marked the order `Failed` and emailed the fan.

The compensating transactions still ran newest first, just like in the Epic Saga. But nobody planned that order:
it falls out of the call stack. Each module undoes its own step after the module below it has answered.

Check each module:

- `GET /payments/{orderId}` is `Refunded`.
- `GET /inventory/holds/{orderId}` is `Released`, and `GET /inventory/events` shows the seat count back where it was.
- `GET /tickets/{orderId}` is `Failed` with `attempts: 1`.
- Papercut has a "could not be completed" email to `unlucky-fan@example.com`.

Note that for a moment the card *was* charged and the seats *were* held. Each module committed its own local
transaction before calling the next one; the saga made the result atomic by undoing them, not by locking all
four databases.

## More failure paths

- **Card declined:** `"paymentToken":"tok_declined"`. Payments fails its own step, so it never calls Ticketing.
  Inventory hears "Failed" and releases the seats.
- **Sold out:** `"eventId":"back-room-sessions","quantity":5`. Inventory fails its own step, so it never calls
  Payments, and there is nothing to undo.
- **Bad request:** `"quantity":0` or an invalid email returns `400` with validation errors.

## Run the tests

```sh
cd samples/phone-tag-saga
dotnet test
```

The tests start the whole AppHost (PostgreSQL and Papercut containers included), so Docker must be running.
[`PhoneTagSagaTests`](tests/TicketTide.EndToEndTests/PhoneTagSagaTests.cs) covers the happy path, the Ticketing
failure and the compensations that travel back along the chain, a declined card, a sold-out event, and validation.

## Things to notice

- **No orchestrator, so no bottleneck.** Nothing sits in the middle of every purchase. Each module only talks to
  its neighbor.
- **Every module carries workflow and undo logic.** Inventory and Payments each know who comes next, and how to
  undo their own step. Both have their own retry loop for their compensation.
- **The workflow is spread out.** To see the whole saga you have to read four handlers. To add a step, or change
  the order, you change several modules and the commands between them. This works for short, linear workflows.
  It gets hard to change as the workflow grows.
- **Commands carry data for strangers.** `HoldSeats` carries a payment token that Inventory never uses, only to
  pass it on.
- **The fan waits for everything.** Response time is the sum of every step, plus compensation on failure. Every
  module must be up, and each module holds its caller waiting until the end of the chain answers.
- **Compensations must be idempotent.** Releasing seats and refunding the card do nothing if there is nothing to
  undo, so they are safe to retry.
- **Compensation can fail too.** If a module cannot undo its step after a few tries, it adds a note to the steps
  and the reply says so. Checkout leaves the order in `NeedsAttention` for a person to fix, and does not tell the
  fan they were not charged.
- **Not crash-safe.** If the process dies mid-chain, nobody knows where the saga stopped. There is no orchestrator
  to resume it, and Checkout only knows the order was placed.

Compare with the [Epic Saga](../epic-saga/), which keeps the synchronous calls and atomicity but puts the whole
workflow in one orchestrator, and the [Time Travel Saga](../time-travel-saga/), which keeps the chain but drops
atomicity: it retries Ticketing instead of undoing the purchase.
