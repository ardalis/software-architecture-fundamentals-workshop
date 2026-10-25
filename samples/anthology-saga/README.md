# Anthology Saga (asynchronous, eventually consistent, choreographed)

| Communication | Consistency | Coordination  |
| ------------- | ----------- | ------------- |
| Asynchronous  | Eventual    | Choreographed |

There is **no orchestrator**. Checkout accepts the order and answers the fan right away. From then on, each module
**reacts to an event and publishes its own**, over RabbitMQ: Checkout publishes Order Placed, Inventory reacts and
publishes Seats Held, Payments reacts and publishes Payment Received, Ticketing reacts and publishes Tickets Issued,
and Checkout reacts by emailing the tickets. No one coordinates, and no one waits.

When Ticketing fails, nothing upstream is undone. Its message goes back through RabbitMQ and is tried again until
Ticketing recovers. The fan already knew the order was in. The tickets just arrive a little later.

Like an anthology of short stories, each module tells its own part and nobody tells the whole thing.
It has the lowest coupling and the highest scalability of the eight sagas. The cost: no one owns the workflow,
so complex flows and their errors are hard to follow and debug.

Animated diagram: [Anthology Saga diagram](../../diagrams/sagas/anthology-saga/index.html)

## The TicketTide workflow

A fan buys tickets for an event:

1. **Inventory** holds the seats.
2. **Payments** charges the card (through a fake payment provider).
3. **Ticketing** issues the tickets.
4. **Checkout** emails the fan (through Papercut, a fake SMTP server).

In this saga, each step starts when the step before it publishes its event.

## How this sample is built

```text
TicketTide.AppHost          Aspire: PostgreSQL (4 databases), RabbitMQ, Papercut, and the API
TicketTide.Api              ASP.NET Core host (FastEndpoints). Wires the modules together.
TicketTide.Messaging        A small RabbitMQ publisher and consumer (see below)
TicketTide.Checkout         Orders, the order history, fan emails     -> checkoutdb, checkout queue
TicketTide.Inventory        Events, seat holds, and prices             -> inventorydb, inventory queue
TicketTide.Payments         Charges (fake provider)                    -> paymentsdb, payments queue
TicketTide.Ticketing        Ticket issuing (with failure injection)    -> ticketingdb, ticketing queue
TicketTide.*.Contracts      The events each module publishes. Public messages only.
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

It is a **modular monolith**: one process, but each module owns its own database and its own queue,
and modules only know each other's `Contracts`. Even though every module runs in the same process,
the saga's events really go through RabbitMQ, so you can watch them.

There are no synchronous calls between modules, so this sample does not use Mediator. Even the price comes
from an event: Checkout does not know the catalog, so Inventory prices the order and puts the amount in Seats Held.

### Who reacts to what

Each module's `Contracts` holds the events it **publishes**. A module references only the contracts of the
modules whose events it **reacts to**:

| Queue       | Subscribes to                                                                                     | Publishes                                          |
| ----------- | ------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| `inventory` | `OrderPlaced`, `PaymentDeclined`                                                                  | `SeatsHeld`, `SeatsRejected`, `SeatsReleased`      |
| `payments`  | `SeatsHeld`                                                                                       | `PaymentReceived`, `PaymentDeclined`               |
| `ticketing` | `PaymentReceived`                                                                                 | `TicketsIssued`                                    |
| `checkout`  | `SeatsHeld`, `SeatsRejected`, `SeatsReleased`, `PaymentReceived`, `PaymentDeclined`, `TicketsIssued` | `OrderPlaced`                                      |

`SeatsHeld` goes to two queues: Payments acts on it, and Checkout records it. RabbitMQ copies the event to every
queue bound to it, and the publisher never knows who is listening.

The saga is spread across one handler per reaction:

- [`PlaceOrderEndpoint`](src/TicketTide.Checkout/Endpoints/OrderEndpoints.cs) saves the order, publishes
  `OrderPlaced`, and answers `202 Accepted`.
- [`OrderPlacedHandler`](src/TicketTide.Inventory/Handlers/OrderPlacedHandler.cs) holds and prices the seats.
- [`SeatsHeldHandler`](src/TicketTide.Payments/Handlers/SeatsHeldHandler.cs) charges the card.
- [`PaymentReceivedHandler`](src/TicketTide.Ticketing/Handlers/PaymentReceivedHandler.cs) issues the tickets,
  or records the failure and throws.
- [`PaymentDeclinedHandler`](src/TicketTide.Inventory/Handlers/PaymentDeclinedHandler.cs) releases the seats.
- [`OrderEventHandlers`](src/TicketTide.Checkout/Saga/OrderEventHandlers.cs) records every event in the order's
  history, confirms or fails the order, and emails the fan.

With no orchestrator to remember the details, the events carry what the next module needs. `OrderPlaced` includes
the payment token and `simulateTicketingFailures`. Inventory passes them on in `SeatsHeld`, and Payments passes the
failure count on in `PaymentReceived`.

Checkout does not run the saga. It only listens, so `GET /orders/{id}` shows the events Checkout has heard about.
Events can arrive close together, so the order uses optimistic concurrency: a conflict throws, and the event is
retried.

### Messaging

[`TicketTide.Messaging`](src/TicketTide.Messaging/) is about 350 lines, so you can see every moving part
instead of a bus library hiding them:

- One topic exchange, `tickettide`. The routing key is the event type's name.
- One durable quorum queue per module, bound to the events that module reacts to.
- Messages are persistent, and publishing waits for RabbitMQ's confirmation.
- A handler that throws does not hold up its queue. The message waits a second in the module's retry queue
  (`ticketing.retry`, for example), then goes back on the module's queue to be tried again. After 20 attempts
  it goes to the `tickettide.dead-letter` queue instead. A message that cannot be read (bad JSON) goes straight there.

That retry queue **is** the retry. Ticketing does not schedule anything. It throws, and the message comes back.

Delivery is *at least once*, so every handler is idempotent. A module saves its work, then publishes its event.
If publishing fails, the message is retried, the handler finds the work already done, and publishes the event again.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/anthology-saga
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
This is "Order Received" in the diagram. Checkout does not know the event name or the price yet:

```json
{
  "orderId": "0199...",
  "status": "Pending",
  "eventName": null,
  "amount": null,
  "history": ["Order received."]
}
```

A moment later, `GET /orders/{orderId}` shows each event as Checkout heard it:

```json
{
  "status": "Confirmed",
  "eventName": "The Midnight Tide",
  "amount": 90.00,
  "ticketCodes": ["TT-0199ABCD-01", "TT-0199ABCD-02"],
  "history": [
    "Order received.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing issued 2 ticket(s).",
    "Order confirmed. Tickets emailed."
  ]
}
```

Check it:

- `GET /inventory/holds/{orderId}` is `Held`, `GET /payments/{orderId}` is `Charged`, `GET /tickets/{orderId}` is `Issued`.
- Papercut has a "Your tickets for The Midnight Tide" email to `fan@example.com`. This is "Tickets Emailed".
- In the dashboard's **Traces** view, follow the RabbitMQ publish and deliver spans from module to module.
  They form a chain, not a fan: each module starts only when the one before it is done.

## Walkthrough 2: Ticketing fails, and its message is retried

`simulateTicketingFailures` makes Ticketing fail that many attempts for this order (up to 10).

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"patient-fan@example.com","paymentToken":"tok_visa","simulateTicketingFailures":5}'
```

The fan still gets `202 Accepted` and `Pending` right away. Inventory holds the seats and Payments charges the card
as before. Then Ticketing fails while handling Payment Received. It records the failed attempt and throws, so the
message waits a second in `ticketing.retry`, then goes back on the `ticketing` queue. Poll `GET /tickets/{orderId}` during the next few seconds:

```json
{
  "status": "Failed",
  "attempts": 2,
  "ticketCodes": [],
  "lastError": "Ticketing is unavailable (simulated failure 2 of 5)."
}
```

Meanwhile:

- In the RabbitMQ management UI, open **Queues and Streams**. Most of the time the message sits in
  `ticketing.retry` (*Ready* is 1), waiting out its second, while `ticketing` itself stays empty and free for other
  orders. Try `"simulateTicketingFailures":10` if you want more time to look.
- In the dashboard's **Structured logs**, each failure is a warning:
  `ticketing failed to handle PaymentReceived (attempt 3); retrying via ticketing.retry`.
- `GET /orders/{orderId}` is still `Pending`, and its history stops at "Payments charged $90.00.". Checkout has no
  idea Ticketing is struggling. It just has not heard Tickets Issued yet.

On the sixth attempt Ticketing succeeds, publishes Tickets Issued, and the order turns `Confirmed`. Its history
is the same as in the happy path. Check it:

- `GET /tickets/{orderId}` is `Issued` with `attempts: 6`.
- `GET /payments/{orderId}` is still `Charged`, and `GET /inventory/holds/{orderId}` is still `Held`.
  Nothing upstream was undone. Compare that with the [Horror Story Saga](../horror-story-saga/), which compensates.
- The tickets email arrives a few seconds later than in the happy path.

## More failure paths: a business "no" still has to be undone

Eventual consistency is about recovering from *technical* failures by retrying, which here means the retry queue.
A declined card or a sold-out event will not fix itself however often you retry, so it is handled with
**compensating events** instead:

- **Card declined:** `"paymentToken":"tok_declined"`. Payments publishes `PaymentDeclined`. Checkout fails the order
  and emails the fan. Inventory reacts to the same event by releasing the seats and publishing `SeatsReleased`, so
  the history ends with "Inventory released the seats." Ticketing never hears of the order (`GET /tickets/{orderId}`
  is `404`).
- **Sold out:** `"eventId":"back-room-sessions","quantity":5`. Back Room Sessions only has 4 seats, so Inventory
  publishes `SeatsRejected` and Checkout fails the order. Payments never hears of it, so there is nothing to undo.
- **Unknown event:** Checkout does not know the catalog, so it accepts the order. Inventory publishes `SeatsRejected`,
  and the order fails.
- **Bad request:** `"quantity":0` or an invalid email returns `400` with validation errors.

## Run the tests

```sh
cd samples/anthology-saga
dotnet test
```

The tests start the whole AppHost (PostgreSQL, RabbitMQ, and Papercut containers), so Docker must be running.
[`AnthologySagaTests`](tests/TicketTide.EndToEndTests/AnthologySagaTests.cs) covers the happy path, Ticketing
failures retried through the retry queue with nothing undone, a declined card (seats released), a sold-out event, an unknown
event, and validation.

## Things to notice

- **Lowest coupling, highest scalability.** No module waits for another, and nothing sits in the middle.
  Payments only knows Inventory's events. Ticketing only knows Payments' events. You could add a module that reacts
  to `TicketsIssued` without changing anyone.
- **No one owns the workflow.** The saga is spread over an endpoint and five handlers in four modules.
  To see the whole flow you read all of them, or follow a trace. Adding a step means changing who listens to what in several places.
- **Errors are hard to follow and debug.** While Ticketing fails, the order just says `Pending`. The story is in
  Ticketing's table, the RabbitMQ UI, and the logs. Nobody watches the saga as a whole.
- **The dead-letter queue is where stuck sagas end up.** After 20 attempts the consumer gives up and moves the message
  to `tickettide.dead-letter`. The order stays `Pending` forever unless someone watches that queue and replays it.
  That is why `simulateTicketingFailures` stops at 10.
- **Events carry state forward.** The payment token travels through Inventory, which has no use for it,
  because no one else will hand it to Payments.
- **Events can arrive out of order.** If Checkout hears Tickets Issued before Seats Held (say, after a retry),
  it throws so the event is retried after Seats Held. Every handler is also safe to run twice.
- **No outbox.** Checkout saves the order, then publishes Order Placed. If the process dies between the two,
  the order stays `Pending`. A production system would use a transactional outbox.

Compare with the [Horror Story Saga](../horror-story-saga/), which is also asynchronous and choreographed but
atomic, so a Ticketing failure is compensated, and the [Parallel Saga](../parallel-saga/), which is also
asynchronous and eventually consistent but has an orchestrator that owns the retries.
