# Fairy Tale Saga (synchronous, eventually consistent, orchestrated)

| Communication | Consistency | Coordination |
| ------------- | ----------- | ------------ |
| Synchronous   | Eventual    | Orchestrated |

A **Checkout orchestrator** calls Inventory, Payments, and Ticketing one at a time and waits for each answer,
just like the [Epic Saga](../epic-saga/). Each module commits its own local transaction right away.
The difference is what happens when a step fails. When Ticketing fails, the orchestrator does **not** undo
the earlier work. It tells the fan the order is **pending**, and then it **retries** Issue Tickets until
Ticketing recovers. For a while the system is out of step (the card is charged, but there are no tickets yet).
In the end it is consistent again: "happily ever after".

One owner, the orchestrator, handles the workflow and its errors. It is a common choice: much less coupling
than an Epic Saga, with the same familiar request/response style.

Animated diagram: [diagrams/sagas/fairy-tale-saga](../../diagrams/sagas/fairy-tale-saga/index.html)

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
TicketTide.Checkout         Orders, the saga orchestrator, the retry service, fan emails   -> checkoutdb
TicketTide.Inventory        Events and seat holds                                          -> inventorydb
TicketTide.Payments         Charges and refunds (fake provider)                            -> paymentsdb
TicketTide.Ticketing        Ticket issuing (with failure injection)                        -> ticketingdb
TicketTide.*.Contracts      The commands each module accepts. Public messages only.
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

It is a **modular monolith**: one process, but each module owns its own database, and modules only
know each other's `Contracts`. Checkout references `TicketTide.Inventory.Contracts`, never
`TicketTide.Inventory`, so it cannot reach into Inventory's tables or classes.

Calls between modules are synchronous, in-process [Mediator](https://github.com/martinothamar/Mediator)
commands (`HoldSeats`, `ChargeCard`, `IssueTickets`, and `ReleaseSeats` when a purchase is rejected).

The saga lives in two classes in Checkout:

- [`CheckoutOrchestrator`](src/TicketTide.Checkout/Saga/CheckoutOrchestrator.cs) runs the steps. If Ticketing
  fails, it saves the order as `Pending`, with the time of the next attempt, and answers the fan.
- [`TicketingRetryService`](src/TicketTide.Checkout/Saga/TicketingRetryService.cs) is a `BackgroundService`.
  Every 250 ms it looks in checkoutdb for pending orders whose next attempt is due and asks the orchestrator
  to retry Issue Tickets. Attempts are about 1 second apart.

The pending state lives in the database, not in memory, so the retries carry on if the API restarts.

The orchestrator saves the order after every step, so `GET /orders/{id}` shows the saga's status and a
step-by-step history, including every retry. The statuses are `Placed`, `SeatsHeld`, `PaymentCharged`,
`Pending` (waiting for tickets), `Confirmed`, and `Failed`.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/fairy-tale-saga
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

This is exactly what the Epic Saga does when nothing goes wrong. Check it:

- `GET /inventory/events` shows 2 fewer seats for `midnight-tide`.
- `GET /inventory/holds/{orderId}` is `Held`, `GET /payments/{orderId}` is `Charged`, `GET /tickets/{orderId}` is `Issued`.
- Papercut has a "Your tickets for The Midnight Tide" email to `fan@example.com`.
- In the dashboard's **Traces** view, the `POST /orders` trace shows each module's database calls, one after another.

## Walkthrough 2: Ticketing fails, so the order is pending and the orchestrator retries

`simulateTicketingFailures` makes Ticketing fail that many attempts for this order. Ask for 3 failures:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"patient-fan@example.com","paymentToken":"tok_visa","simulateTicketingFailures":3}'
```

The fan does not wait for Ticketing to recover. The answer comes right away: `202 Accepted`, with a `Location`
header that points at the order. The order is in, and the tickets are on the way:

```json
{
  "orderId": "0199...",
  "status": "Pending",
  "amount": 90.00,
  "ticketCodes": [],
  "failureReason": null,
  "history": [
    "Order placed.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 1 of 3). Will retry."
  ]
}
```

Watch the order. Run this a few times over the next few seconds:

```sh
curl http://localhost:5240/orders/{orderId}
```

Each retry adds a line to the history. After about 3 seconds the order is `Confirmed`:

```json
{
  "status": "Confirmed",
  "ticketCodes": ["TT-0199ABCD-01", "TT-0199ABCD-02"],
  "history": [
    "Order placed.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 1 of 3). Will retry.",
    "Retry 1: Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 2 of 3). Will retry.",
    "Retry 2: Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 3 of 3). Will retry.",
    "Retry 3: Ticketing issued 2 ticket(s). Tickets confirmed."
  ]
}
```

Check each module:

- `GET /tickets/{orderId}` is `Issued` with `attempts: 4`. Run it while the order is still pending to see the
  count go up, with `status: Failed` and the `lastError` of the latest attempt.
- `GET /payments/{orderId}` is still `Charged`. The card was never refunded.
- `GET /inventory/holds/{orderId}` is still `Held`. The seats were never released.
- Papercut gets a "Your tickets for The Midnight Tide" email to `patient-fan@example.com`, but only once the
  tickets exist, a few seconds after the fan got the `202`.

Compare this with the Epic Saga. The same failure there makes the orchestrator **compensate**: it refunds the
card, releases the seats, and the fan gets `422` and a "could not be completed" email. Here, the orchestrator
keeps the work that already succeeded and moves forward instead.

### Restart while an order is pending

The retry is durable. Place an order with `"simulateTicketingFailures":10`, then restart the `api` resource from
the Aspire dashboard while the order is still `Pending`. PostgreSQL keeps running, so when the API comes back,
`TicketingRetryService` finds the pending order in checkoutdb and carries on retrying until it is `Confirmed`.

## More failure paths

Eventual consistency is about recovering from **technical** failures: Ticketing is down, so try again later.
A **business** "no" is different. A retry will not change it, so the orchestrator still has to handle it.

- **Card declined:** `"paymentToken":"tok_declined"`. The orchestrator does not retry. It releases the seats,
  the order is `Failed` (`422`), and the fan gets a "could not be completed" email. Ticketing is never called.
- **Sold out:** `"eventId":"back-room-sessions","quantity":5`. Inventory rejects the hold, so there is nothing to
  undo. The order is `Failed` (`422`).
- **Bad request:** `"quantity":0` or an invalid email returns `400` with validation errors.

## Run the tests

```sh
cd samples/fairy-tale-saga
dotnet test
```

The tests start the whole AppHost (PostgreSQL and Papercut containers included), so Docker must be running.
[`FairyTaleSagaTests`](tests/TicketTide.EndToEndTests/FairyTaleSagaTests.cs) covers the happy path, the Ticketing
failure (`202 Pending`, then polling `GET /orders/{id}` until `Confirmed`, with nothing undone), a declined card,
a sold-out event, and validation.

## Things to notice

- **One owner handles the workflow and its errors.** Inventory, Payments, and Ticketing never retry or undo
  anything themselves. The orchestrator decides what a failure means and what to do next.
- **Much less coupling than an Epic Saga, with the same request/response style.** The calls are still synchronous,
  but Ticketing no longer has to be up for a purchase to go through. If it is down, orders pile up as `Pending`
  and complete when it recovers.
- **Users see in-between states.** The fan, the UI, and support staff must all understand `Pending`. Here the
  card is charged and the seats are held, but there are no tickets yet. That is the price of eventual consistency.
- **Retried steps must be idempotent.** `IssueTickets` returns the tickets already issued if it is called again,
  so a retry after a lost answer cannot issue a second set.
- **A business failure is not retried.** A declined card ends the order and releases the seats. Only technical
  failures are worth retrying.
- **Keep the retrier simple, then harden it.** This one waits a fixed 1 second and retries forever, which keeps the
  demo fast. A production retrier would add jitter and a growing backoff, stop after a limit and alert someone,
  and claim rows (for example with `FOR UPDATE SKIP LOCKED`) when more than one API instance runs.
- **Not fully crash-safe.** The retry service picks up `Pending` orders. If the process dies between two earlier
  steps, the order is left in its last saved state, as in the Epic Saga. A production orchestrator would resume
  those too.

Compare with:

- The [Epic Saga](../epic-saga/), which keeps the orchestrator and synchronous calls but is atomic: one Ticketing
  failure undoes the whole purchase.
- The [Parallel Saga](../parallel-saga/), which keeps the orchestrator and eventual consistency but makes the calls
  asynchronous through RabbitMQ, so the fan never waits for any module.
