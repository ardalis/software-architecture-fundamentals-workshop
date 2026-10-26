# Atomic Modular Monolith

TicketTide's ticket sales, organized by **module**, with real boundaries between the modules, and **one SQL Server
database** they all share. Because they share it, one ACID transaction can cover the work of every module, so a
failure part-way through needs no saga: the rollback undoes everything at once.

The [Clean Architecture](../clean-architecture/) and [Vertical Slice](../vertical-slice/) samples have exactly the
same API and behavior, and the same end-to-end tests. Compare it with the [saga samples](../README.md#saga-patterns)
too: they are modular monoliths as well, but each module has its own database, so a failure needs compensating.

## What it does

A fan buys tickets for an event, and TicketTide support can refund an order:

| Request | What happens |
| --- | --- |
| `GET /events` | The events on sale, and the seats each has left |
| `POST /orders` | Sells the seats, issues one ticket per seat, charges the card (a fake provider), and saves the order, all in one transaction, then emails the fan. `201` with the order, or `422` with the order if the card is declined |
| `GET /orders/{orderId}` | The order and its tickets |
| `POST /orders/{orderId}/refund` | Marks the order and every ticket `Refunded`, returns the seats, and refunds the card, all in one transaction, then emails the fan |

Broken business rules (not enough seats, refunding an order that isn't paid) return `400`, unknown events and
orders return `404`, and losing a race for the last seats returns `400` or `409`.

One extra switch, for the demo: `"simulateTicketingFailure": true` in the order request makes the Tickets module
crash after it has written the tickets. You get a `500`, and nothing is left behind.

## Step 1: find the module boundaries

The other monolith samples have one `AppDbContext` with `Event`, `Order`, and `Ticket` in it. Asking "who owns this
data, and who else needs to change it?" splits it into four modules:

| Module | Owns | Others ask it to |
| --- | --- | --- |
| **Events** | The events on sale, and how many seats each has sold | Say what an event costs and how many seats are left (`GetEventDetails`), sell seats (`SellSeats`), return them (`ReturnSeats`) |
| **Tickets** | The tickets a fan holds, and whether each still works | Issue tickets for an order (`IssueTickets`), refund them (`RefundTickets`), list them (`GetTicketsForOrder`) |
| **Orders** | A fan's order: what they bought, what they paid, and its status | Nothing. It runs the use cases, and tells the others when an order commits (`OrderPlaced`, `OrderRefunded`) |
| **EmailSending** | Talking to the SMTP server | Send an email (`SendEmail`) |

Each module has its own tables in its own schema (`events.Events`, `tickets.Tickets`, `orders.Orders`) with no
foreign keys between them, and its own DbContext, which can't see any other module's tables. The order refers to
its event and its tickets by id only. The payment provider is an external system that only Orders talks to, so it
lives inside Orders rather than in a module of its own.

## How it is built

The layout follows [modularmonoliths.com](https://modularmonoliths.com) and [RiverBooks](https://github.com/ardalis/RiverBooks):

```text
src/
  Events/
    TicketTide.Events.Contracts    The module's public messages. Other modules may reference only this.
    TicketTide.Events.Internal     Everything else: entities, DbContext, handlers, endpoints
      Integrations/                  Handlers for the messages in Contracts (what other modules call)
      UseCases/                      Handlers for the module's own messages (what its endpoints call)
    TicketTide.Events.Tests        The module on its own
  Orders/        (the same three projects)
  Tickets/       (the same three projects)
  EmailSending/  (the same three projects)
  TicketTide.SharedKernel          The unit of work, the Mediator behaviors, and Result-to-HTTP mapping
  TicketTide.Web                   The host: the only project that references the Internal projects
  TicketTide.AppHost               Aspire: SQL Server, Papercut (a fake SMTP server), and the web app
tests/
  TicketTide.IntegrationTests      Every module together, on SQLite: the transactions, without Docker
  TicketTide.EndToEndTests         Starts the whole AppHost and drives it over HTTP
```

**The boundary rule:** an Internal project references other modules' Contracts projects, never their Internal
projects. Orders sends `SellSeats` (Events.Contracts) and `IssueTickets` (Tickets.Contracts). It can't touch the
`Event` or `Ticket` entities or their DbContexts. Each module's tests check that rule.

Modules talk in-process through [Mediator](https://github.com/martinothamar/Mediator) (source generated)
commands, queries, and notifications. Every message goes through two **behaviors** in
[SharedKernel](src/TicketTide.SharedKernel/Behaviors/):

- [`LoggingBehavior`](src/TicketTide.SharedKernel/Behaviors/LoggingBehavior.cs) logs each message and starts a trace
  span for it, saying whether it ran **inside the transaction**. In the Aspire dashboard's Traces view, one
  `POST /orders` shows `SellSeats` and `IssueTickets` inside the transaction, and their SQL on the same connection.
- [`ValidationBehavior`](src/TicketTide.SharedKernel/Behaviors/ValidationBehavior.cs) runs each module's
  FluentValidation validators for its Contracts. Any module can send a module's messages, so each module guards its
  own front door. HTTP requests are validated by their FastEndpoints validators first, so a fan's mistake is a `400`.

## Step 2: one transaction across the modules

Every module's DbContext derives from [`ModuleDbContext`](src/TicketTide.SharedKernel/Data/ModuleDbContext.cs), and
they all share the request's **one `SqlConnection`**. The [`UnitOfWork`](src/TicketTide.SharedKernel/Data/UnitOfWork.cs)
starts a transaction on that connection and enlists every module's DbContext in it, including those created later.
The modules don't know it exists. They save their own changes as usual, and those changes become permanent only
when the unit of work commits.

The use case that starts the work runs the transaction. Simplified from [`PlaceOrderHandler`](src/Orders/TicketTide.Orders.Internal/UseCases/PlaceOrder/PlaceOrderHandler.cs):

```csharp
await using (var transaction = await unitOfWork.BeginTransactionAsync(ct))
{
    await sender.Send(new SellSeats(...));          // 1. Events
    await sender.Send(new IssueTickets(...));       // 2. Tickets
    charge = await payments.ChargeAsync(...);       // 3. The payment provider, last
    orders.Add(order); await orders.SaveChanges();  // 4. Orders
    await transaction.CommitAsync();                // All of it, at the same moment
}
await publisher.PublishAsync(new OrderPlaced(...)); // After the commit: Tickets emails the fan
```

What happens when something fails:

| Fails at | What the fan sees | What's undone, and how |
| --- | --- | --- |
| Events (sold out) | `400` | Nothing was written |
| Tickets (`simulateTicketingFailure`) | `500` | The rollback removes the sold seats and the written tickets. The card was never charged |
| The payment provider (declined) | `422`, a declined order | The rollback removes the seats and tickets. Then the declined order is saved on its own |
| Saving the order, or the commit | `500` | The rollback removes the seats and tickets, and **the card is refunded**: the only compensation in the sample |

Compare that with the [Epic Saga](../epic-saga/): the same modules, but each with its own database. There, every
step commits on its own, so a failed step means running a compensating command for every step before it, newest
first, and handling a compensation that fails too. Here, nothing was committed, so there's nothing to compensate.

### Why it's ordered like that

- **The payment provider is outside the database**, so no rollback can undo a charge. Charging last, after every
  module's work has succeeded, means a failure in any module happens before the card is charged.
- **Email and other side effects happen after the commit.** Orders publishes `OrderPlaced` only after committing.
  The Tickets module handles it and sends the email through EmailSending. If the email went out inside the
  transaction, a later rollback would leave the fan holding tickets that don't exist.
- **Explicit, not a behavior.** A Mediator behavior could wrap every command in a transaction, but a declined card
  needs a rollback *and then* a save, which a behavior can't express. So the use case runs the transaction itself.

### The price

- **The modules share a database.** Every module that takes part in a transaction has to stay in the same SQL
  Server database, on the same server. Splitting a module out into its own service, or its own database, means
  replacing the transaction with a saga.
- **Locks are held for the whole transaction.** `SellSeats` locks the event's row until the commit, including while
  the payment provider answers. That's how two fans racing for the last seats are kept apart (the second one waits,
  then finds them gone), but on a busy event it also queues every buyer behind the slowest card payment.

## Where things live

| What | Where |
| --- | --- |
| "You can't buy more seats than are left" | [`SellSeatsHandler`](src/Events/TicketTide.Events.Internal/Integrations/SellSeatsHandler.cs) (one conditional `UPDATE`) |
| "Only a paid order can be refunded" | [`Order.Refund`](src/Orders/TicketTide.Orders.Internal/Data/Order.cs) |
| The steps of buying tickets, and the transaction | [`PlaceOrderHandler`](src/Orders/TicketTide.Orders.Internal/UseCases/PlaceOrder/PlaceOrderHandler.cs) |
| The steps of a refund, and the transaction | [`RefundOrderHandler`](src/Orders/TicketTide.Orders.Internal/UseCases/RefundOrder/RefundOrderHandler.cs) |
| The one connection, and the transaction every module joins | [`UnitOfWork`](src/TicketTide.SharedKernel/Data/UnitOfWork.cs) and [`ModuleDbContext`](src/TicketTide.SharedKernel/Data/ModuleDbContext.cs) |
| The email text | [`OrderEmailHandlers`](src/Tickets/TicketTide.Tickets.Internal/Integrations/OrderEmailHandlers.cs), after the commit |
| Wiring the modules together | [`Program.cs`](src/TicketTide.Web/Program.cs) |

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/atomic-modular-monolith
dotnet run --project src/TicketTide.AppHost
```

The SQL Server image is for x64. On Apple Silicon, turn on "Use Rosetta for x86_64/amd64 emulation" in Docker
Desktop's settings.

Open the Aspire dashboard link printed in the console. SQL Server takes longer to start than PostgreSQL, so give
it a minute. When `api` is healthy, the API is at `http://localhost:5240`, and Papercut's inbox is the `papercut`
resource's `http` endpoint. The home page, [`http://localhost:5240/`](http://localhost:5240/), has a button for the
happy path, a refund, each failure path, and **Ticketing fails**, plus a form for your own orders and an order
lookup. [`TicketTide.Web.http`](src/TicketTide.Web/TicketTide.Web.http) has every request. The database starts empty
every run, apart from three seeded events.

This sample uses the same ports as the other samples, so run one at a time.

## Walkthrough: a failure with nothing to undo

Note the seats left for The Midnight Tide (`GET /events`), then buy two tickets with Ticketing crashing:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"fan@example.com","paymentToken":"tok_visa","simulateTicketingFailure":true}'
```

You get `500`: "Tickets failed, so the whole purchase was rolled back...". Check `GET /events` again: the same
number of seats. Papercut has no email. Open the request in the dashboard's **Traces** view: `SellSeats` ran and
updated the event, `IssueTickets` ran and inserted two tickets, then the transaction rolled back, and no
compensating command ran.

Now buy them for real (drop `simulateTicketingFailure`). You get `201`, the seats go down by two, and after the
commit, Papercut has a "Your tickets for The Midnight Tide" email.

Questions to ask while you read it:

- `PlaceOrderHandler` changes three modules' data. How many lines handle a failure? Compare the Epic Saga's
  `CheckoutOrchestrator`.
- What would change if the Tickets module moved to its own database? Its own service?
- Why is the card charged *after* the tickets are issued, when a person would do it the other way round?
- What would you do to stop a slow payment provider from holding the event's row lock?

## Tests

From the repository root:

```sh
dotnet BuildTestFormat.cs atomic-modular-monolith             # build, all tests, format check
dotnet BuildTestFormat.cs atomic-modular-monolith --no-test   # skip the tests
```

Or just the tests that don't need Docker:

```sh
cd samples/atomic-modular-monolith
dotnet test --project tests/TicketTide.IntegrationTests       # every module together, on SQLite
dotnet test --project src/Events/TicketTide.Events.Tests      # or any module's own tests
```

- **Each module's `Tests` project** tests that module on its own, and checks it uses only other modules' Contracts.
- **`TicketTide.IntegrationTests`** composes every module the way the web app does, on one in-memory SQLite
  connection, and fails each step in turn: the seats, tickets, and order all roll back together, and only a failure
  after the charge refunds the card.
- **`TicketTide.EndToEndTests`** are the same as the other monolith samples' tests, plus the Ticketing failure, against
  SQL Server and Papercut in containers.

## What it leaves out

The same as the other samples: no authentication (anyone who can guess an order id can refund it), a fake payment
provider, and tables created on startup instead of migrations. Email is best effort: it's sent after the commit, and
a failed email is logged, not retried. If the process dies between the commit and the email, the fan gets no email.
The same shared database makes the fix cheap: a **transactional outbox**, a table the integration events are
written to inside the transaction and sent from afterwards. If the process dies between charging the card and
committing, the card isn't refunded.
