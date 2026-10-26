# Clean Architecture

TicketTide's ticket sales, organized by **layer**, with every dependency pointing inward toward the domain.

The [Vertical Slice sample](../vertical-slice/) has exactly the same API and behavior, and the same end-to-end
tests. Only the code's structure differs, so you can compare the two styles directly. The
[add-a-feature exercise](../../exercises/add-a-feature.md) adds ticket transfer to both.

## What it does

A fan buys tickets for an event, and TicketTide support can refund an order:

| Request | What happens |
| --- | --- |
| `GET /events` | The events on sale, and the seats each has left |
| `POST /orders` | Checks the seats, charges the card (a fake provider), sells the seats, issues one ticket per seat, and emails the fan. `201` with the order, or `422` with the order if the card is declined |
| `GET /orders/{orderId}` | The order and its tickets |
| `POST /orders/{orderId}/refund` | Refunds the card, returns the seats, marks every ticket `Refunded`, and emails the fan |

Broken business rules (not enough seats, refunding an order that isn't paid) return `400`, unknown events and
orders return `404`, and losing a race for the last seats returns `409`.

## How it is built

```text
TicketTide.Core             The domain model, its rules, its interfaces, and the use cases. No dependencies.
TicketTide.Infrastructure   Implements Core's interfaces: EF Core + PostgreSQL, the payment provider, email
TicketTide.Web              FastEndpoints endpoints, and the composition root that wires it all together
TicketTide.AppHost          Aspire: PostgreSQL, Papercut (a fake SMTP server), and the web app
tests/TicketTide.UnitTests       Core's rules and use cases, with in-memory stand-ins. No Docker needed.
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

**The dependency rule:** Web depends on Core and Infrastructure, Infrastructure depends on Core, and Core
depends on nothing. Core declares what it needs from the outside world as interfaces (`IOrderRepository`,
`IPaymentGateway`, `IFanNotifier`, ...), and Infrastructure implements them. That's dependency inversion at the
project level: the database and email are details that plug into the domain, not the other way around.

### Where things live

| What | Where |
| --- | --- |
| "A paid order issues one ticket per seat" | [`Order.MarkPaid`](src/TicketTide.Core/OrderAggregate/Order.cs) |
| "Only a paid order can be refunded" | [`Order.Refund`](src/TicketTide.Core/OrderAggregate/Order.cs) |
| "You can't buy more seats than are left" | [`Event.SellSeats`](src/TicketTide.Core/EventAggregate/Event.cs) |
| The steps of buying tickets | [`PlaceOrderService`](src/TicketTide.Core/Services/PlaceOrderService.cs), behind [`IPlaceOrderService`](src/TicketTide.Core/Services/IPlaceOrderService.cs) |
| The steps of a refund | [`RefundOrderService`](src/TicketTide.Core/Services/RefundOrderService.cs), behind [`IRefundOrderService`](src/TicketTide.Core/Services/IRefundOrderService.cs) |
| Saving, and spotting two fans racing for the same seats | [`AppDbContext`](src/TicketTide.Infrastructure/Data/AppDbContext.cs) |
| The email text | [`SmtpFanNotifier`](src/TicketTide.Infrastructure/Email/SmtpFanNotifier.cs) |
| HTTP: routes, validation, status codes | [`Web/Orders`](src/TicketTide.Web/Orders/) and [`Web/Events`](src/TicketTide.Web/Events/) |

`Order` is an **aggregate**: its tickets can only change through it (`Ticket`'s methods are `internal`), and the
repository always loads an order with its tickets. Rules return [Ardalis.Result](https://github.com/ardalis/Result)
values instead of throwing, and [`ResultExtensions`](src/TicketTide.Web/ResultExtensions.cs) turns a failed result
into the right HTTP status.

This sample follows the conventions of the [Ardalis.CleanArchitecture template](https://github.com/ardalis/CleanArchitecture),
trimmed to fit a workshop. The template also has a separate UseCases project (commands and queries through a
mediator), specifications, and domain events.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/clean-architecture
dotnet run --project src/TicketTide.AppHost
```

Open the Aspire dashboard link printed in the console. When `api` is healthy, the API is at
`http://localhost:5240`, and Papercut's inbox is the `papercut` resource's `http` endpoint.
The home page, [`http://localhost:5240/`](http://localhost:5240/), has a button for the happy path, a refund, and
each failure path, a form for your own orders, and an order lookup, and shows every request and response.
[`TicketTide.Web.http`](src/TicketTide.Web/TicketTide.Web.http) has every request, ready to run in Visual Studio,
VS Code (REST Client), or Rider. The database starts empty every run, apart from three seeded events.

This sample uses the same ports as the other samples, so run one at a time.

## Walkthrough: follow one request through the layers

Buy two tickets:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"fan@example.com","paymentToken":"tok_visa"}'
```

You get `201 Created` with the order and two tickets, and Papercut has a "Your tickets for The Midnight Tide"
email. Now follow the request through the code:

1. **Web:** [`PlaceOrder`](src/TicketTide.Web/Orders/PlaceOrder.cs) validates the request, calls
   `IPlaceOrderService`, and maps the result to `201`, `422`, or an error. No business logic.
2. **Core:** [`PlaceOrderService`](src/TicketTide.Core/Services/PlaceOrderService.cs) loads the event through
   `IEventRepository`, asks it whether the seats are available, charges the card through `IPaymentGateway`, and
   tells the order it was paid. It doesn't know about PostgreSQL, SMTP, or HTTP.
3. **Core:** [`Order.MarkPaid`](src/TicketTide.Core/OrderAggregate/Order.cs) issues the tickets.
4. **Infrastructure:** [`AppDbContext`](src/TicketTide.Infrastructure/Data/AppDbContext.cs) saves the order and
   the event in one transaction, and [`SmtpFanNotifier`](src/TicketTide.Infrastructure/Email/SmtpFanNotifier.cs)
   sends the email.

Then refund it, twice:

```sh
curl -i -X POST http://localhost:5240/orders/{orderId}/refund
curl -i -X POST http://localhost:5240/orders/{orderId}/refund
```

The first returns `200` with every ticket `Refunded`. The second returns `400`: the rule in `Order.Refund`
says only a paid order can be refunded.

Questions to ask while you read it:

- How many files did one request touch? How many projects?
- If the business changed the refund rule, how many places would you look?
- What would it take to swap PostgreSQL for another database? To test `PlaceOrderService` without one?
  (See [`ServiceTests`](tests/TicketTide.UnitTests/ServiceTests.cs).)

## Tests

From the repository root:

```sh
dotnet BuildTestFormat.cs clean-architecture             # build, all tests, format check
dotnet BuildTestFormat.cs clean-architecture --no-test   # skip the tests
```

Or just the unit tests, which don't need Docker:

```sh
cd samples/clean-architecture
dotnet test --project tests/TicketTide.UnitTests
```

## What it leaves out

The same as the other samples: no authentication (anyone who can guess an order id can refund it), a fake
payment provider, and a database created on startup with `EnsureCreated` instead of migrations. If saving an order
fails after the card is charged, the card is refunded, but if the process dies in between, it isn't. Email is best
effort: the order is saved first, and a failed email is logged, not retried. A refund puts the seats back with one
`UPDATE` statement after the order is saved, so it never conflicts with fans buying seats at the same moment.
