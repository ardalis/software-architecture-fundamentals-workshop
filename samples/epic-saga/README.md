# Epic Saga (synchronous, atomic, orchestrated)

| Communication | Consistency | Coordination |
| ------------- | ----------- | ------------ |
| Synchronous   | Atomic      | Orchestrated |

A **Checkout orchestrator** calls Inventory, Payments, and Ticketing one at a time and waits for each answer.
If any step fails, it sends **compensating commands** for the steps that already succeeded, newest first,
and only then answers the fan. The fan waits for the whole saga. Either everything happens, or nothing does.

This is the saga that behaves most like a database transaction in a monolith. It is easy to follow,
but it is tightly coupled: every module must be up, and the fan waits for all of them.

Animated diagram: [diagrams/sagas/epic-saga](../../diagrams/sagas/epic-saga/index.html)

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
TicketTide.Checkout         Orders, the saga orchestrator, fan emails        -> checkoutdb
TicketTide.Inventory        Events and seat holds                            -> inventorydb
TicketTide.Payments         Charges and refunds (fake provider)              -> paymentsdb
TicketTide.Ticketing        Ticket issuing (with failure injection)          -> ticketingdb
TicketTide.*.Contracts      The commands each module accepts. Public messages only.
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

It is a **modular monolith**: one process, but each module owns its own database, and modules only
know each other's `Contracts`. Checkout references `TicketTide.Inventory.Contracts`, never
`TicketTide.Inventory`, so it cannot reach into Inventory's tables or classes.

Calls between modules are synchronous, in-process [Mediator](https://github.com/martinothamar/Mediator)
commands (`HoldSeats`, `ChargeCard`, `IssueTickets`) and compensations (`ReleaseSeats`, `RefundCard`).

The saga lives in [`CheckoutOrchestrator`](src/TicketTide.Checkout/Saga/CheckoutOrchestrator.cs).
It saves the order after every step, so `GET /orders/{id}` shows the saga's status and a step-by-step history.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/epic-saga
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

The request blocks until the saga finishes, then returns `201 Created`:

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
    "Ticketing issued 2 ticket(s). Tickets confirmed."
  ]
}
```

Check it:

- `GET /inventory/events` shows 2 fewer seats for `midnight-tide`.
- `GET /inventory/holds/{orderId}` is `Held`, `GET /payments/{orderId}` is `Charged`, `GET /tickets/{orderId}` is `Issued`.
- Papercut has a "Your tickets for The Midnight Tide" email to `fan@example.com`.
- In the dashboard's **Traces** view, the `POST /orders` trace shows each module's database calls, one after another.

## Walkthrough 2: Ticketing fails, so the saga compensates

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
    "Ticketing could not issue the tickets: ...",
    "Compensating: undoing the steps that succeeded, newest first.",
    "Payments refunded the card.",
    "Inventory released the seats.",
    "Purchase failed."
  ]
}
```

The compensating transactions ran in reverse order. Check each module:

- `GET /payments/{orderId}` is `Refunded`.
- `GET /inventory/holds/{orderId}` is `Released`, and `GET /inventory/events` shows the seat count back where it was.
- `GET /tickets/{orderId}` is `Failed` with `attempts: 1`.
- Papercut has a "could not be completed" email to `unlucky-fan@example.com`.

Note that for a moment the card *was* charged and the seats *were* held. Each module committed its own local
transaction; the saga made the result atomic by undoing them, not by locking all four databases.

## More failure paths

- **Card declined:** `"paymentToken":"tok_declined"`. Inventory releases the seats; Ticketing is never called.
- **Sold out:** `"eventId":"back-room-sessions","quantity":5`. Inventory rejects the hold, so there is nothing to undo.
- **Bad request:** `"quantity":0` or an invalid email returns `400` with validation errors.

## Run the tests

```sh
cd samples/epic-saga
dotnet test
```

The tests start the whole AppHost (PostgreSQL and Papercut containers included), so Docker must be running.
[`EpicSagaTests`](tests/TicketTide.EndToEndTests/EpicSagaTests.cs) covers the happy path, the Ticketing failure
and its compensations, a declined card, a sold-out event, and validation.

## Things to notice

- **The fan waits for everything.** Response time is the sum of every step, plus compensation on failure.
- **Every module must be up.** If Payments is down, nobody can buy tickets.
- **Compensations must be idempotent.** `ReleaseSeats` and `RefundCard` do nothing if there is nothing to undo, so
  they are safe to retry.
- **Compensation can fail too.** If a compensation keeps failing, the order stays `Compensating` with a note in its
  history, for a person to fix. A production system would retry it from a background job.
- **Not crash-safe.** If the process dies mid-saga, the order is left in its last saved state. A production
  orchestrator would resume or compensate unfinished sagas on startup.

Compare with the [Fairy Tale Saga](../fairy-tale-saga/), which keeps the orchestrator and synchronous calls but
drops atomicity: it retries Ticketing instead of undoing the purchase.
