# Parallel Saga (asynchronous, eventually consistent, orchestrated)

| Communication | Consistency | Coordination |
| ------------- | ----------- | ------------ |
| Asynchronous  | Eventual    | Orchestrated |

A **Checkout orchestrator** accepts the order and answers the fan right away. Then it sends
Hold Seats, Charge Card, and Issue Tickets to all three modules **at the same time**, as messages on RabbitMQ,
and does not wait. Each module works at its own pace and replies when it is done. When all three have
succeeded, the fan gets the tickets by email.

When Ticketing fails, nothing is rolled back. The orchestrator owns the error, so it retries until
Ticketing recovers. Until then, the fan sees the order as `Pending`.

You get high throughput and still have one owner for errors. The costs: the orchestrator must track saga state,
and users see in-between states like "pending".

Animated diagram: [Parallel Saga diagram](../../diagrams/sagas/parallel-saga/index.html)

## The TicketTide workflow

A fan buys tickets for an event:

1. **Inventory** holds the seats.
2. **Payments** charges the card (through a fake payment provider).
3. **Ticketing** issues the tickets.
4. **Checkout** emails the fan (through Papercut, a fake SMTP server).

In this saga, steps 1 to 3 run in parallel.

## How this sample is built

```text
TicketTide.AppHost          Aspire: PostgreSQL (4 databases), RabbitMQ, Papercut, and the API
TicketTide.Api              ASP.NET Core host (FastEndpoints + Mediator). Wires the modules together.
TicketTide.Messaging        A small RabbitMQ publisher and consumer (see below)
TicketTide.Checkout         Orders, the saga orchestrator, the retry worker, fan emails  -> checkoutdb, checkout queue
TicketTide.Inventory        Events and seat holds                                          -> inventorydb, inventory queue
TicketTide.Payments         Charges and refunds (fake provider)                            -> paymentsdb, payments queue
TicketTide.Ticketing        Ticket issuing (with failure injection)                        -> ticketingdb, ticketing queue
TicketTide.*.Contracts      The messages each module accepts and the replies it publishes
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

It is a **modular monolith**: one process, but each module owns its own database and its own queue,
and modules only know each other's `Contracts`. Even though every module runs in the same process,
the saga's messages really go through RabbitMQ, so you can watch them, and a slow module never blocks the others.

The only synchronous call is `GetEventPrice`, an in-process [Mediator](https://github.com/martinothamar/Mediator)
query. Checkout prices the order *before* the saga starts, because Payments needs the amount at the same moment
Inventory gets the hold request.

The saga lives in [`CheckoutOrchestrator`](src/TicketTide.Checkout/Saga/CheckoutOrchestrator.cs).
Replies arrive on the `checkout` queue in any order, so the order tracks each step separately
(`SeatsHeld`, `PaymentCharged`, `TicketsIssued`). Retries are scheduled on the order itself and sent by
[`TicketingRetryWorker`](src/TicketTide.Checkout/Saga/TicketingRetryWorker.cs), so they survive a restart.

### Messaging

[`TicketTide.Messaging`](src/TicketTide.Messaging/) is about 350 lines, so you can see every moving part
instead of a bus library hiding them:

- One topic exchange, `tickettide`. The routing key is the message type's name.
- One durable quorum queue per module, bound to the messages that module handles.
- Messages are persistent, and publishing waits for RabbitMQ's confirmation.
- A handler that throws does not hold up its queue. The message waits a second in the module's retry queue
  (`checkout.retry`, for example), then goes back on the module's queue to be tried again. After 20 attempts
  it goes to the `tickettide.dead-letter` queue instead. A message that cannot be read (bad JSON) goes straight there.

Delivery is *at least once*, so every handler is idempotent. A module saves its work, then publishes its reply.
If publishing fails, the message is retried, the handler finds the work already done, and publishes the reply again.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/parallel-saga
dotnet run --project src/TicketTide.AppHost
```

Open the Aspire dashboard link printed in the console. When `api` is healthy:

- The API is at `http://localhost:5240`.
- The home page, [`http://localhost:5240/`](http://localhost:5240/), has a button for each walkthrough below and a form
  for your own orders. It shows the order's history and what each module recorded.
- Papercut's inbox is the `papercut` resource's `http` endpoint in the dashboard.
- RabbitMQ's management UI is the `rabbitmq` resource's `management` endpoint. Sign in as `guest`, with the
  password from the `rabbitmq` resource's `RABBITMQ_DEFAULT_PASS` environment variable in the dashboard.
- [`TicketTide.Api.http`](src/TicketTide.Api/TicketTide.Api.http) has every request below.

The databases and queues are not persistent: every run starts with fresh data.

## Walkthrough 1: the happy path

Buy two tickets for The Midnight Tide:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"fan@example.com","paymentToken":"tok_visa"}'
```

The answer comes back at once: `202 Accepted`, with a `Location` header pointing at the order.

```json
{
  "orderId": "0199...",
  "status": "Pending",
  "amount": 90.00,
  "history": [
    "Order placed.",
    "Sent Hold Seats, Charge Card, and Issue Tickets in parallel."
  ]
}
```

A moment later, `GET /orders/{orderId}` shows the replies in the order they arrived, for example:

```json
{
  "status": "Confirmed",
  "ticketCodes": ["TT-0199ABCD-01", "TT-0199ABCD-02"],
  "history": [
    "Order placed.",
    "Sent Hold Seats, Charge Card, and Issue Tickets in parallel.",
    "Ticketing issued 2 ticket(s).",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "All three steps are done. Tickets emailed."
  ]
}
```

Check it:

- `GET /inventory/holds/{orderId}` is `Held`, `GET /payments/{orderId}` is `Charged`, `GET /tickets/{orderId}` is `Issued`.
- Papercut has a "Your tickets for The Midnight Tide" email to `fan@example.com`.
- In the dashboard's **Traces** view, the trace for `POST /orders` fans out into three RabbitMQ deliveries that
  overlap in time, then three replies.

## Walkthrough 2: Ticketing fails, so the orchestrator retries

`simulateTicketingFailures` makes Ticketing fail that many attempts for this order.

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"patient-fan@example.com","paymentToken":"tok_visa","simulateTicketingFailures":3}'
```

The fan still gets `202 Accepted` and `Pending` right away. Poll `GET /orders/{orderId}` for a few seconds:

```json
{
  "status": "Confirmed",
  "history": [
    "Order placed.",
    "Sent Hold Seats, Charge Card, and Issue Tickets in parallel.",
    "Ticketing failed: Ticketing is unavailable (simulated failure 1 of 3). Will retry.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Retry 1: sent Issue Tickets again.",
    "Ticketing failed: Ticketing is unavailable (simulated failure 2 of 3). Will retry.",
    "Retry 2: sent Issue Tickets again.",
    "Ticketing failed: Ticketing is unavailable (simulated failure 3 of 3). Will retry.",
    "Retry 3: sent Issue Tickets again.",
    "Ticketing issued 2 ticket(s).",
    "All three steps are done. Tickets emailed."
  ]
}
```

Check it:

- `GET /payments/{orderId}` is still `Charged`, and `GET /inventory/holds/{orderId}` is still `Held`.
  Nothing was rolled back. Compare that with the [Epic Saga](../epic-saga/), which refunds and releases.
- `GET /tickets/{orderId}` shows `attempts: 4`.
- The tickets email arrives only once the tickets exist. Until then the fan saw `Pending`.

## Walkthrough 3: a business "no" still has to be undone

Eventual consistency is about recovering from *technical* failures by retrying. A declined card or a sold-out
event will not fix itself, so the order fails, and the orchestrator undoes the other steps.

Because the steps run in parallel, the other modules may already have done their part:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"back-room-sessions","quantity":5,"fanEmail":"late-fan@example.com","paymentToken":"tok_visa"}'
```

Back Room Sessions only has 4 seats, so Inventory says no. But Payments may already have charged the card,
and Ticketing may already have issued tickets. The orchestrator sends `ReleaseSeats`, `RefundCard`, and
`VoidTickets`. The order is `Failed`; `GET /payments/{orderId}` ends `Refunded` (or `Cancelled`, if the refund
overtook the charge, see below) and `GET /tickets/{orderId}` ends `Voided`.

A compensation can even arrive *before* the command it undoes. Each module handles that with a "tombstone":
`RefundCard` for an order Payments has never seen records a `Cancelled` payment, so a late `ChargeCard` never
charges the card. Try `"paymentToken":"tok_declined"` too: the seats are released and the tickets voided.

## Run the tests

```sh
cd samples/parallel-saga
dotnet test
```

The tests start the whole AppHost (PostgreSQL, RabbitMQ, and Papercut containers), so Docker must be running.
[`ParallelSagaTests`](tests/TicketTide.EndToEndTests/ParallelSagaTests.cs) covers the happy path, Ticketing
retries without compensation, a declined card and a sold-out event (with the parallel steps undone), an unknown
event, and validation.

## Things to notice

- **The fan never waits for the slowest module.** The response is immediate; the email comes later.
- **Replies arrive in any order.** The orchestrator cannot assume Inventory answers first.
- **The orchestrator owns errors.** Retries live in one place, and the order's history shows every one.
- **In-between states are visible.** The fan, support staff, and reports all see `Pending` orders.
- **Parallel steps cost more to undo.** Here the card can be charged for an event that was already sold out.
  Running steps one after another (the [Fairy Tale Saga](../fairy-tale-saga/)) avoids that, but is slower.
- **No outbox.** Checkout saves the order, then publishes the commands. If the process dies between the two,
  the order stays `Pending`. A production system would use a transactional outbox.

Compare with the [Fantasy Fiction Saga](../fantasy-fiction-saga/), which keeps the parallel messages but makes
the saga atomic, and the [Anthology Saga](../anthology-saga/), which removes the orchestrator.
