# Horror Story Saga (asynchronous, atomic, choreographed)

| Communication | Consistency | Coordination  |
| ------------- | ----------- | ------------- |
| Asynchronous  | Atomic      | Choreographed |

There is **no orchestrator**. Each module publishes an **event** to RabbitMQ when it finishes its step, and the next
module reacts to it: Checkout publishes Order Placed, Inventory holds the seats and publishes Seats Held, Payments
charges the card and publishes Payment Received, and Ticketing issues the tickets and publishes Tickets Issued.
No one coordinates, and no module waits for another.

But the saga is **atomic**. If Ticketing fails, every step before it must be undone, and there is nobody in the
middle to do it. So Ticketing's failure must reach **every service upstream**. Payments refunds the card and
Inventory releases the seats, at the same time, and their messages race each other. Checkout must collect every
compensation before it can tell the fan anything. The fan waits for all of it.

It pairs the loosest coordination with the strictest consistency. That is why it is a horror story.

Animated diagram: [Horror Story Saga diagram](../../diagrams/sagas/horror-story-saga/index.html)

## The TicketTide workflow

A fan buys tickets for an event:

1. **Inventory** holds the seats.
2. **Payments** charges the card (through a fake payment provider).
3. **Ticketing** issues the tickets.
4. **Checkout** emails the fan (through Papercut, a fake SMTP server).

In this saga, each step starts when the step before it publishes its event, and the fan waits for all of them.

## How this sample is built

```text
TicketTide.AppHost          Aspire: PostgreSQL (4 databases), RabbitMQ, Papercut, and the API
TicketTide.Api              ASP.NET Core host (FastEndpoints). Wires the modules together.
TicketTide.Messaging        A small RabbitMQ publisher and consumer (see below)
TicketTide.Checkout         Orders, the waiting fan, fan emails                   -> checkoutdb, checkout queue
TicketTide.Inventory        Seat holds; releases seats when a later step fails    -> inventorydb, inventory queue
TicketTide.Payments         Charges; refunds the card when Ticketing fails        -> paymentsdb, payments queue
TicketTide.Ticketing        Ticket issuing (with failure injection)               -> ticketingdb, ticketing queue
TicketTide.*.Contracts      The events each module publishes. Public messages only.
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

It is a **modular monolith**: one process, but each module owns its own database and its own queue, and modules
only know each other's `Contracts`. Each `Contracts` project holds the events that module **publishes**. A module
references the contracts of the modules whose events it **consumes**:

```text
Checkout  -> Inventory, Payments, Ticketing contracts  (it listens to every event, for the order's history)
Inventory -> Checkout, Payments, Ticketing contracts   (Order Placed, and every failure downstream of it)
Payments  -> Inventory, Ticketing contracts            (Seats Held, and Ticketing's failure)
Ticketing -> Payments contracts                        (Payment Received)
```

Look at Inventory: it is the second step in the chain, but it must know about every service after it. That is the
price of atomicity without an orchestrator.

### Messaging

The modules still run in one process, but every event goes through RabbitMQ.
[`TicketTide.Messaging`](src/TicketTide.Messaging/) is about 350 lines, so you can see every moving part
instead of a bus library hiding them:

- One topic exchange, `tickettide`. The routing key is the event type's name.
- One durable quorum queue per module, bound to the events that module handles.
- Messages are persistent, and publishing waits for RabbitMQ's confirmation.
- A handler that throws does not hold up its queue. The message waits a second in the module's retry queue
  (`inventory.retry`, for example), then goes back on the module's queue to be tried again. After 20 attempts
  it goes to the `tickettide.dead-letter` queue instead. A message that cannot be read (bad JSON) goes straight there.

One event can be bound to several queues, which is how publish/subscribe works here:

| Event (publisher)               | `inventory` | `payments` | `ticketing` | `checkout` |
| ------------------------------- | :---------: | :--------: | :---------: | :--------: |
| `OrderPlaced` (Checkout)        | yes         |            |             |            |
| `SeatsHeld` (Inventory)         |             | yes        |             | history    |
| `SeatsRejected` (Inventory)     |             |            |             | yes        |
| `PaymentReceived` (Payments)    |             |            | yes         | history    |
| `PaymentDeclined` (Payments)    | yes         |            |             | history    |
| `TicketsIssued` (Ticketing)     |             |            |             | yes        |
| `TicketIssueFailed` (Ticketing) | yes         | yes        |             | history    |
| `CardRefunded` (Payments)       | yes         |            |             | history    |
| `SeatsReleased` (Inventory)     |             |            |             | yes        |

"history" means Checkout only writes the event down in the order's history. Only three events finish an order:
`TicketsIssued` (confirmed), `SeatsRejected` (failed, nothing to undo), and `SeatsReleased` (failed, everything
undone).

With no orchestrator to remember the details, the events carry what the rest of the chain needs. `OrderPlaced`
includes the payment token (for Payments) and `simulateTicketingFailures` (for Ticketing). Inventory knows the price,
so `SeatsHeld` adds the amount. Checkout never asks anyone for the price.

Delivery is *at least once*, so every handler is idempotent: a retried event finds its work already done and
publishes the same outcome again.

### How the fan waits

`POST /orders` saves the order, publishes `OrderPlaced`, and then waits. The waiting is done by
[`OrderCompletionSignal`](src/TicketTide.Checkout/Saga/OrderCompletionSignal.cs): the endpoint registers the order
before it publishes, and the event handler that moves the order to `Confirmed` or `Failed` completes it. The endpoint
then returns `201 Created` or `422 Unprocessable Entity`. If the saga is still running after 30 seconds, it returns
`202 Accepted` with the order so far, and the fan can poll `GET /orders/{id}` or wait for the email.

The signal lives in memory, so it only works because every module runs in one process. With several API instances,
the event that finishes the order could arrive at a different instance than the one the fan is waiting on.

The order statuses are `Pending`, `Confirmed`, `Compensating` (Checkout has heard a failure and is waiting for
the compensations), and `Failed`.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/horror-story-saga
dotnet run --project src/TicketTide.AppHost
```

Open the Aspire dashboard link printed in the console. When `api` is healthy:

- The API is at `http://localhost:5240`.
- The home page, [`http://localhost:5240/`](http://localhost:5240/), has a button for each walkthrough below and a form
  for your own orders. It shows the order's history and what each module recorded.
- Papercut's inbox is the `papercut` resource's `http` endpoint in the dashboard.
- RabbitMQ's management UI is the `rabbitmq` resource's `management` endpoint. Sign in as `guest`, with the
  password from the `rabbitmq` resource's `RABBITMQ_DEFAULT_PASS` environment variable in the dashboard.
  The **Queues and Streams** tab shows the four module queues, their retry queues, and the dead-letter queue, and
  each module queue's **Bindings** show the events it subscribes to.
- [`TicketTide.Api.http`](src/TicketTide.Api/TicketTide.Api.http) has every request below.

The databases and queues are not persistent: every run starts with fresh data.

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

The request blocks while the events travel along the chain, then returns `201 Created`:

```json
{
  "orderId": "01a1...",
  "status": "Confirmed",
  "eventName": "The Midnight Tide",
  "amount": 90.00,
  "ticketCodes": ["TT-01A16234-01", "TT-01A16234-02"],
  "history": [
    "Order placed. Checkout published Order Placed.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing issued 2 ticket(s).",
    "Tickets confirmed. Tickets emailed."
  ]
}
```

Check it:

- `GET /inventory/events` shows 2 fewer seats for `midnight-tide`.
- `GET /inventory/holds/{orderId}` is `Held`, `GET /payments/{orderId}` is `Charged`, `GET /tickets/{orderId}` is `Issued`.
- Papercut has a "Your tickets for The Midnight Tide" email to `fan@example.com`.
- In the dashboard's **Traces** view, follow the RabbitMQ publish and deliver spans from module to module.
  They form a chain: each module starts only when the one before it is done.

## Walkthrough 2: Ticketing fails, and the compensations race each other

`simulateTicketingFailures` makes Ticketing fail for this order. An atomic saga does not retry; one failure undoes
the whole purchase.

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"unlucky-fan@example.com","paymentToken":"tok_visa","simulateTicketingFailures":1}'
```

Here is what happens, with no one in charge:

1. Ticketing fails and publishes `TicketIssueFailed`. It is bound to both the `payments` and the `inventory`
   queues, so both hear it at the same time.
2. Payments refunds the card and publishes `CardRefunded`.
3. Inventory releases the seats. But it cannot report yet: Checkout tells the fan as soon as it hears
   `SeatsReleased`, and the refund might still be in flight. So Inventory waits until it has heard **both**
   `TicketIssueFailed` and `CardRefunded`.
4. The two events race. Inventory may hear about the refund before it hears about the failure (for example, if its
   first try at handling the failure throws and the message is retried a second later). So Inventory writes
   down each event in `inventorydb` as it arrives, and releases the seats on whichever comes first. When it has
   both, it publishes `SeatsReleased`, with the events it heard in the order they arrived.
5. Checkout hears `SeatsReleased`, the last compensation, marks the order `Failed`, and only then answers the fan.

The fan waits, then gets `422 Unprocessable Entity`:

```json
{
  "status": "Failed",
  "failureReason": "Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 1 of 1).",
  "history": [
    "Order placed. Checkout published Order Placed.",
    "Inventory held 2 seat(s) for The Midnight Tide.",
    "Payments charged $90.00.",
    "Ticketing could not issue the tickets: Ticketing is unavailable (simulated failure 1 of 1).",
    "Payments refunded the card.",
    "Inventory released the seats. It heard TicketIssueFailed, then CardRefunded.",
    "Every compensation is in. Purchase failed."
  ]
}
```

For the fraction of a second while the compensations run, `GET /orders/{orderId}` shows `Compensating`.
Check each module:

- `GET /payments/{orderId}` is `Refunded`.
- `GET /inventory/holds/{orderId}` is `Released`, and `GET /inventory/events` shows the seat count back where it was.
- `GET /tickets/{orderId}` is `Failed` with `attempts: 1`.
- Papercut has a "could not be completed" email to `unlucky-fan@example.com`.

The code for the race is in
[`ReleaseSeatsHandlers`](src/TicketTide.Inventory/Handlers/ReleaseSeatsHandlers.cs). Compare it with the
[Epic Saga](../epic-saga/), where the orchestrator simply calls the compensations one after another.

## More failure paths

- **Card declined:** `"paymentToken":"tok_declined"`. Payments publishes `PaymentDeclined`. Inventory releases the
  seats and publishes `SeatsReleased` right away, because nothing else was done. Ticketing never hears about the order.
- **Sold out:** `"eventId":"back-room-sessions","quantity":5`. Inventory publishes `SeatsRejected`, so the chain stops
  there and there is nothing to undo. The card is never charged.
- **Bad request:** `"quantity":0` or an invalid email returns `400` with validation errors.

## Run the tests

```sh
cd samples/horror-story-saga
dotnet test
```

The tests start the whole AppHost (PostgreSQL, RabbitMQ, and Papercut containers), so Docker must be
running. [`HorrorStorySagaTests`](tests/TicketTide.EndToEndTests/HorrorStorySagaTests.cs) covers the happy path,
the Ticketing failure and its compensations, a declined card, a sold-out event, an unknown event, and validation.

## Things to notice

- **It pairs the loosest coordination with the strictest consistency.** Choreography and asynchronous messages
  decouple the modules. Atomicity couples them right back together.
- **Every service must know about every service upstream.** Ticketing's failure has to reach Payments and
  Inventory, and Inventory has to listen to events from Payments and Ticketing, not just from Checkout. Add a step to
  the chain and every module before it may need to change.
- **Nobody knows when the saga is over.** Inventory has to work out for itself when every compensation is in, and
  Checkout has to wait for the last one. Without the saga's state in one place, a missed event leaves the order
  `Compensating` with no one to notice. A production system would need a timeout and a person to look at it.
- **Compensations race.** Events can arrive in any order, and any handler can run twice. Every handler records what
  it has done and what it has heard, so the order of arrival does not matter.
- **The fan still waits.** The steps are asynchronous, but the fan's request is not. Holding the request open
  while events travel through RabbitMQ only works here because everything runs in one process.

If you end up here, change one dimension. Relax atomicity and you get the [Anthology Saga](../anthology-saga/):
the same events, but a failure is retried instead of undone. Or add an orchestrator and you get the
[Fantasy Fiction Saga](../fantasy-fiction-saga/): still asynchronous and atomic, but one place knows the whole
workflow and runs the compensations.
