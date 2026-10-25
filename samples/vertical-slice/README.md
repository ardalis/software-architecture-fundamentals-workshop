# Vertical Slice Architecture

TicketTide's ticket sales, organized by **feature**: each request is one folder with everything it needs.

The [Clean Architecture sample](../clean-architecture/) has exactly the same API and behavior, and the same
end-to-end tests. Only the code's structure differs, so you can compare the two styles directly. The
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
TicketTide.Web
  Features/
    ListEvents/      GET /events
    PlaceOrder/      POST /orders: request, validator, and the whole use case
    GetOrder/        GET /orders/{orderId}
    RefundOrder/     POST /orders/{orderId}/refund
    OrderResponse.cs The order as the API returns it, shared by the slices that return one
  Data/              The EF Core DbContext and plain entity classes
  Infrastructure/    The payment provider and email, shared by the slices
TicketTide.AppHost   Aspire: PostgreSQL, Papercut (a fake SMTP server), and the web app
tests/TicketTide.EndToEndTests   Starts the whole AppHost and drives it over HTTP
```

Each slice is a [FastEndpoints](https://fast-endpoints.com/) endpoint in the **REPR** shape (Request, Endpoint,
Response). The endpoint's `HandleAsync` is the handler: it reads and writes the database directly and holds the
feature's rules. Slices share as little as possible. The entities in `Data/` are plain data with public setters,
so each slice decides how they change.

### Where things live

| What | Where |
| --- | --- |
| "A paid order issues one ticket per seat" | [`PlaceOrderEndpoint`](src/TicketTide.Web/Features/PlaceOrder/PlaceOrder.cs) |
| "You can't buy more seats than are left" | [`PlaceOrderEndpoint`](src/TicketTide.Web/Features/PlaceOrder/PlaceOrder.cs) |
| "Only a paid order can be refunded" | [`RefundOrderEndpoint`](src/TicketTide.Web/Features/RefundOrder/RefundOrder.cs) |
| The email text | In each slice, next to the logic that sends it |
| Spotting two fans racing for the same seats | The `Version` columns in [`AppDbContext`](src/TicketTide.Web/Data/AppDbContext.cs), caught in each slice |

Compare that table with the [Clean Architecture sample's](../clean-architecture/README.md#where-things-live):
there, the rules are on the `Order` and `Event` aggregates, wherever they are used from.

## Run it

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running.

```sh
cd samples/vertical-slice
dotnet run --project src/TicketTide.AppHost
```

Open the Aspire dashboard link printed in the console. When `api` is healthy, the API is at
`http://localhost:5240`, and Papercut's inbox is the `papercut` resource's `http` endpoint.
The home page, [`http://localhost:5240/`](http://localhost:5240/), has a button for the happy path, a refund, and
each failure path, a form for your own orders, and an order lookup, and shows every request and response.
[`TicketTide.Web.http`](src/TicketTide.Web/TicketTide.Web.http) has every request, ready to run in Visual Studio,
VS Code (REST Client), or Rider. The database starts empty every run, apart from three seeded events.

This sample uses the same ports as the other samples, so run one at a time.

## Walkthrough: one feature, one folder

Buy two tickets:

```sh
curl -i -X POST http://localhost:5240/orders \
  -H "Content-Type: application/json" \
  -d '{"eventId":"midnight-tide","quantity":2,"fanEmail":"fan@example.com","paymentToken":"tok_visa"}'
```

You get `201 Created` with the order and two tickets, and Papercut has a "Your tickets for The Midnight Tide"
email. Now open [`Features/PlaceOrder/PlaceOrder.cs`](src/TicketTide.Web/Features/PlaceOrder/PlaceOrder.cs).
That's the whole feature, top to bottom: the request, its validation, the seat check, the charge, the tickets,
the save, the email, and the response.

Then refund it, twice:

```sh
curl -i -X POST http://localhost:5240/orders/{orderId}/refund
curl -i -X POST http://localhost:5240/orders/{orderId}/refund
```

The first returns `200` with every ticket `Refunded`. The second returns `400`: the check at the top of
`RefundOrderEndpoint` says only a paid order can be refunded.

Questions to ask while you read it:

- How many files did one request touch? How many would a new feature add?
- Which code do `PlaceOrder` and `RefundOrder` both need? Is anything already duplicated?
- Where would a rule that applies to *every* feature that changes an order go?

## Tests

From the repository root:

```sh
dotnet BuildTestFormat.cs vertical-slice             # build, the tests, format check
dotnet BuildTestFormat.cs vertical-slice --no-test   # skip the tests
```

The tests are end-to-end only, and identical to the Clean Architecture sample's. With the logic in the
endpoints, there's no domain layer to unit test on its own; each slice is tested through HTTP.

## What it leaves out

The same as the other samples: no authentication (anyone who can guess an order id can refund it), a fake
payment provider, and a database created on startup with `EnsureCreated` instead of migrations. If saving an order
fails after the card is charged, the card is refunded, but if the process dies in between, it isn't. Email is best
effort: the order is saved first, and a failed email is logged, not retried. A refund puts the seats back with one
`UPDATE` statement after the order is saved, so it never conflicts with fans buying seats at the same moment.
