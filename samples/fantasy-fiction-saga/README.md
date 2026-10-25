# Fantasy Fiction Saga (asynchronous, atomic, orchestrated)

| Communication | Consistency | Coordination |
| ------------- | ----------- | ------------ |
| Asynchronous  | Atomic      | Orchestrated |

A **Checkout orchestrator** sends Hold Seats, Charge Card, and Issue Tickets to all three modules
**at the same time**, as messages on RabbitMQ, and does not wait for replies. The replies come back in
whatever order the modules finish.

The saga is also **atomic**. There are no retries: if any step fails, the whole purchase is undone.
The orchestrator sends compensating commands for every step that succeeded. It also keeps tracking the
requests still in flight, because a step can succeed *after* another one failed, and then it must be undone too.
The fan waits the whole time. There is no answer until every reply is in and every compensation is confirmed.

It sounds like the best of both worlds: parallel speed and all-or-nothing safety. In practice, atomicity forces
the orchestrator to track every pending request and every race. That is why it is called the Fantasy Fiction Saga.

Animated diagram: [Fantasy Fiction Saga diagram](../../diagrams/sagas/fantasy-fiction-saga/index.html)

## The TicketTide workflow

A fan buys tickets for an event:

1. **Inventory** holds the seats.
2. **Payments** charges the card (through a fake payment provider).
3. **Ticketing** issues the tickets.
4. **Checkout** emails the fan (through Papercut, a fake SMTP server).

In this saga, steps 1 to 3 run in parallel, and the fan waits for all of them.

## How this sample is built

```text
TicketTide.AppHost          Aspire: PostgreSQL (4 databases), RabbitMQ, Papercut, and the API
TicketTide.Api              ASP.NET Core host (FastEndpoints + Mediator). Wires the modules together.
TicketTide.Messaging        A small RabbitMQ publisher and consumer (see below)
TicketTide.Checkout         Orders, the saga orchestrator, fan emails        -> checkoutdb, checkout queue
TicketTide.Inventory        Events and seat holds                            -> inventorydb, inventory queue
TicketTide.Payments         Charges and refunds (fake provider)              -> paymentsdb, payments queue
TicketTide.Ticketing        Ticket issuing (with failure injection)          -> ticketingdb, ticketing queue
TicketTide.*.Contracts      The messages each module accepts and the replies it publishes
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

It is a **modular monolith**: one process, but each module owns its own database and its own queue,
and modules only know each other's `Contracts`. Even though every module runs in the same process,
the saga's messages really go through RabbitMQ, so you can watch them.

The only synchronous call is `GetEventPrice`, an in-process [Mediator](https://github.com/martinothamar/Mediator)
query. Checkout prices the order *before* the saga starts, because Payments needs the amount at the same moment
Inventory gets the hold request.

The saga lives in [`CheckoutOrchestrator`](src/TicketTide.Checkout/Saga/CheckoutOrchestrator.cs).
Replies arrive on the `checkout` queue in any order. The [`Order`](src/TicketTide.Checkout/Data/Order.cs)
tracks each of the three steps with its own state:

```text
Requested  -> Succeeded                      the reply said yes
Requested  -> Failed                         the reply said no; nothing to undo
Succeeded  -> Compensating -> Compensated    another step failed, so this one is undone
Requested  -> Compensating -> Compensated    a late "yes" after another step failed
```

The order is `Pending` while the requests are out, `Compensating` after the first failure, and only `Failed`
once no step is `Requested` or `Compensating`. That last rule is the whole point: an atomic saga cannot answer
the fan while anything is still in flight.

### How the fan waits

`POST /orders` holds the request open while the replies are handled on the `checkout` queue.
[`OrderCompletionSignal`](src/TicketTide.Checkout/Saga/OrderCompletionSignal.cs) connects the two:
the orchestrator registers the order before it sends anything, and completes it when the order is `Confirmed`
or `Failed`. The endpoint waits up to 30 seconds, reads the final order, and answers `201` or `422`.
If the saga is still running after 30 seconds, it answers `202 Accepted` with the order as it is, and the fan
can poll `GET /orders/{id}`.

This in-memory signal only works because the API is **one process**, so the reply is always handled where the
fan is waiting. With several API instances, another instance could handle the reply. You would need a reply
queue per instance, or the waiting request would have to poll the database.

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
The orchestrator ignores a reply for a step that has already answered, with one exception: while the order is
`Compensating`, it sends every compensation still in flight again. The orchestrator saves its state before it sends
the compensations, so if sending one failed, the retried reply is the only thing that can get it sent.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/fantasy-fiction-saga
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

The messages run in parallel, but the request waits until all three replies are in. Then it returns
`201 Created`. The history shows the replies in the order they arrived, for example:

```json
{
  "orderId": "0199...",
  "status": "Confirmed",
  "eventName": "The Midnight Tide",
  "amount": 90.00,
  "ticketCodes": ["TT-0199ABCD-01", "TT-0199ABCD-02"],
  "history": [
    "Order placed.",
    "Sent Hold Seats, Charge Card, and Issue Tickets in parallel. Waiting for all three replies.",
    "Ticketing issued 2 ticket(s).",
    "Payments charged $90.00.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "All three steps are done. Tickets confirmed."
  ]
}
```

Run it again and the replies may come back in a different order.

Check it:

- `GET /inventory/holds/{orderId}` is `Held`, `GET /payments/{orderId}` is `Charged`, `GET /tickets/{orderId}` is `Issued`.
- Papercut has a "Your tickets for The Midnight Tide" email to `fan@example.com`.
- In the dashboard's **Traces** view, the trace for `POST /orders` fans out into three RabbitMQ deliveries that
  overlap in time, then three replies.

## Walkthrough 2: Ticketing fails, and a late reply is undone

`simulateTicketingFailures` makes Ticketing fail that many attempts for this order. An atomic saga does not
retry; one failure undoes the whole purchase.

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"unlucky-fan@example.com","paymentToken":"tok_visa","simulateTicketingFailures":1}'
```

The fan waits, then gets `422 Unprocessable Entity`. A typical history:

```json
{
  "status": "Failed",
  "failureReason": "Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 1 of 1).",
  "ticketCodes": [],
  "history": [
    "Order placed.",
    "Sent Hold Seats, Charge Card, and Issue Tickets in parallel. Waiting for all three replies.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 1 of 1).",
    "Compensating: undoing every step that succeeded, and waiting for the replies still in flight.",
    "Sent Release Seats.",
    "Late reply: Payments charged $90.00. The purchase is being undone, so sent Refund Card.",
    "Inventory released the seats.",
    "Payments refunded the card.",
    "Every step is undone. Purchase failed."
  ]
}
```

Read it step by step:

1. Inventory answered first, so the seats were held.
2. Ticketing failed. The orchestrator moved the order to `Compensating` and sent Release Seats for the one step
   that had succeeded.
3. Payments had not answered yet. Its request was still in flight, so the orchestrator could not answer the fan.
4. Payments' "charged" reply arrived late. The card really was charged, so the orchestrator sent Refund Card.
5. Only when both compensations were confirmed did the order become `Failed`, and the fan got an answer.

Which reply is late depends on timing. Run the request a few times: sometimes the order is different, but the
end result is always the same. Check each module:

- `GET /payments/{orderId}` is `Refunded`.
- `GET /inventory/holds/{orderId}` is `Released`, and `GET /inventory/events` shows the seat count back where it was.
- `GET /tickets/{orderId}` is `Failed` with `attempts: 1`. No retry.
- Papercut has a "could not be completed" email to `unlucky-fan@example.com`.

Compare that with the [Parallel Saga](../parallel-saga/): the same failure there is retried, nothing is undone,
and the fan is told `Pending` at once.

## More failure paths

- **Card declined:** `"paymentToken":"tok_declined"`. Inventory and Ticketing usually succeed in parallel, so the
  seats are released and the tickets voided. Payments is `Declined`.
- **Sold out:** `"eventId":"back-room-sessions","quantity":5`. Back Room Sessions only has 4 seats, so Inventory
  says no. Payments and Ticketing have already done their part, or do it a moment later. The card is refunded and
  the tickets voided before the fan gets `422`.
- **Unknown event:** an `eventId` that does not exist is rejected with `422` before any message is sent.
- **Bad request:** `"quantity":0` or an invalid email returns `400` with validation errors.

The orchestrator only sends a compensation once the step's success reply has arrived. So in this saga, a
compensation normally never overtakes its command. The modules still keep the "tombstones" from the Parallel Saga
(for example, `RefundCard` for an unknown order records a `Cancelled` payment), because a retried message can
still arrive out of order.

## Run the tests

```sh
cd samples/fantasy-fiction-saga
dotnet test
```

The tests start the whole AppHost (PostgreSQL, RabbitMQ, and Papercut containers), so Docker must be running.
[`FantasyFictionSagaTests`](tests/TicketTide.EndToEndTests/FantasyFictionSagaTests.cs) covers the happy path,
the Ticketing failure and its compensations, a declined card, a sold-out event, an unknown event, and validation.
None of the failure tests need to poll: when `POST /orders` returns, every compensation is already done.

## Things to notice

- **Atomicity forces the orchestrator to track every pending request and race.** Compare
  [`CheckoutOrchestrator`](src/TicketTide.Checkout/Saga/CheckoutOrchestrator.cs) with the Parallel Saga's. It needs
  a state per step, a `Compensating` order status, and a rule for every reply in every state.
- **The fan waits for the slowest module.** And on failure, also for every compensation. Async messaging buys
  parallel work, but not a faster answer.
- **A late success must be undone.** You cannot cancel a message that is already being handled. You can only wait
  for its reply and compensate.
- **Holding the request open is fragile.** It needs the in-memory signal, a timeout, and an honest `202` fallback.
  With more than one API instance it needs more machinery still.
- **No outbox.** Checkout saves the order, then publishes the commands. If the process dies between the two,
  the order stays `Pending` (or `Compensating`). A production system would use a transactional outbox, and resume
  unfinished sagas on startup.

This is usually a sign to pick a different trade-off. Drop atomicity and you get the
[Parallel Saga](../parallel-saga/): the same parallel messages, but it retries instead of undoing, and answers at once.
Drop async and you get the [Epic Saga](../epic-saga/): the same all-or-nothing result, one step at a time, with much
simpler code.
