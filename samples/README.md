# Samples

Runnable architecture samples for the workshop. Each sample is in its own folder.

## Monolithic styles

Three samples build the same small slice of TicketTide, buying tickets and refunding them, in three different
monolithic styles. They have the same API, the same behavior, and the same end-to-end tests. Only the structure of
the code differs. The [add-a-feature exercise](../exercises/add-a-feature.md) adds ticket transfer to Clean
Architecture and Vertical Slice and compares the two.

| Sample | Organized by | Where the business rules live | Projects |
| ------ | ------------ | ----------------------------- | -------- |
| [Clean Architecture](clean-architecture/) | Layer: Core, Infrastructure, Web | On the `Order` and `Event` aggregates in Core | Core, Infrastructure, Web (+ AppHost) |
| [Vertical Slice](vertical-slice/) | Feature: one folder per request | In each feature's endpoint | Web (+ AppHost) |
| [Atomic Modular Monolith](atomic-modular-monolith/) | Module: Events, Orders, Tickets, EmailSending | In each module, which others reach only through its Contracts | Contracts, Internal, and Tests per module, SharedKernel, Web (+ AppHost) |

All three use .NET 10, Aspire, Papercut, and FastEndpoints, like the saga samples, and serve the same API:
`GET /events`, `POST /orders`, `GET /orders/{orderId}`, and `POST /orders/{orderId}/refund`. Clean Architecture
and Vertical Slice use PostgreSQL. The Atomic Modular Monolith uses SQL Server, with every module's tables in one
database, so one transaction covers all the modules and a failure needs no saga. Compare it with the
[Epic Saga](epic-saga/), where the same modules each have their own database. All three serve the same home page at
`/`, linked from [`shared/MonolithHomePage`](shared/MonolithHomePage/): buttons for the happy path, a refund, and the
failure paths, a form for your own orders, and an order lookup. Or use each sample's `.http` file or the `curl`
commands in its README.

## Saga patterns

*Software Architecture: The Hard Parts* (Ford, Richards, Sadalage, Dehghani) names eight sagas, one for each
combination of three choices:

- **Communication:** synchronous (the caller waits) or asynchronous (messages, nobody waits).
- **Consistency:** atomic (all or nothing, so failures are undone) or eventual (failures are retried until the data catches up).
- **Coordination:** orchestrated (one service runs the workflow) or choreographed (each service knows the next step).

There is one sample for each. They all tell the same TicketTide story as the
[animated saga diagrams](../diagrams/sagas/index.html): a fan buys tickets, so **Inventory** holds the seats,
**Payments** charges the card, **Ticketing** issues the tickets, and **Checkout** tells the fan.
Each walkthrough plays the happy path, then the same purchase with Ticketing failing.

| Sample | Communication | Consistency | Coordination | When Ticketing fails | The fan gets |
| ------ | ------------- | ----------- | ------------ | -------------------- | ------------ |
| [Epic Saga](epic-saga/) | Synchronous | Atomic | Orchestrated | The orchestrator refunds the card and releases the seats | The final answer (waits) |
| [Phone Tag Saga](phone-tag-saga/) | Synchronous | Atomic | Choreographed | The failure travels back along the chain; each module undoes its own step | The final answer (waits) |
| [Fairy Tale Saga](fairy-tale-saga/) | Synchronous | Eventual | Orchestrated | The orchestrator retries Ticketing in the background | `Pending`, then an email |
| [Time Travel Saga](time-travel-saga/) | Synchronous | Eventual | Choreographed | Payments retries Ticketing (inline, then in the background) | Usually the final answer; `Pending` in a long outage |
| [Fantasy Fiction Saga](fantasy-fiction-saga/) | Asynchronous | Atomic | Orchestrated | The orchestrator undoes every step, including replies still in flight | The final answer (waits) |
| [Horror Story Saga](horror-story-saga/) | Asynchronous | Atomic | Choreographed | Ticketing's failure event reaches every upstream service; compensations race | The final answer (waits) |
| [Parallel Saga](parallel-saga/) | Asynchronous | Eventual | Orchestrated | The orchestrator retries Ticketing | `Pending` at once, then an email |
| [Anthology Saga](anthology-saga/) | Asynchronous | Eventual | Choreographed | The message goes to Ticketing's retry queue and comes back a second later | `Pending` at once, then an email |

### What every sample has in common

The samples are as alike as possible, so you can diff any two of them and see only the saga.

- **.NET 10 and [Aspire](https://aspire.dev/) 13.** The AppHost starts everything: the API, PostgreSQL,
  [Papercut](https://github.com/ChangemakerStudios/Papercut-SMTP) (a fake SMTP server), and RabbitMQ for the
  asynchronous samples.
- **A modular monolith.** One ASP.NET Core process (`TicketTide.Api`, using [FastEndpoints](https://fast-endpoints.com/))
  hosts four modules: Checkout, Inventory, Payments, and Ticketing. Each module has its own PostgreSQL database
  and a `*.Contracts` project with the only types other modules may use. A module never references another
  module's implementation project.
- **Synchronous calls** between modules are in-process [Mediator](https://github.com/martinothamar/Mediator)
  (source generated) commands. **Asynchronous calls** are messages on RabbitMQ, through a small
  `TicketTide.Messaging` project, so you can see every moving part.
- **The same API.** `POST /orders`, `GET /orders/{id}` (with a step-by-step `history` of the saga),
  `GET /inventory/events`, `GET /inventory/holds/{orderId}`, `GET /payments/{orderId}`, and `GET /tickets/{orderId}`.
- **The same failure switches** in the order request:
  - `"simulateTicketingFailures": N` makes Ticketing fail the first N attempts for that order.
  - `"paymentToken": "tok_declined"` makes the fake payment provider decline the card (`tok_visa` succeeds).
  - `"eventId": "back-room-sessions", "quantity": 5` is more seats than the event has.
- **The same home page.** `GET /` serves a page with buttons for the happy path and the failure paths. Every
  sample links it from [`shared/HomePage`](shared/HomePage/), so change it there and every sample gets the change.
  Its **See the diagram** button plays the saga's animated diagram in a dialog. The API serves the diagram files
  from [`diagrams/sagas`](../diagrams/sagas/) at `/diagrams/sagas/...` and loads the diagram engine from the CDN,
  so the diagram needs internet.
- **End-to-end tests** that start the whole AppHost (containers included) and drive it over HTTP, and a README
  walkthrough of a happy path and a failure path.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker Desktop (or Podman), running
- The [Aspire CLI](https://aspire.dev/get-started/install-cli/)
- Optional: an editor that runs `.http` files (Visual Studio, VS Code with REST Client, or Rider)

The AppHost's default launch profile uses HTTPS for the Aspire dashboard. If you have never trusted the .NET
development certificate on this machine, do it once:

```sh
dotnet dev-certs https --trust
```

Or skip HTTPS with `dotnet run --project src/TicketTide.AppHost --launch-profile http`.

The first run pulls the PostgreSQL, SQL Server, Papercut, and RabbitMQ images (whichever the sample uses), which takes
a few minutes.

## Run a sample

```sh
cd samples/epic-saga            # or any other sample folder
dotnet run --project src/TicketTide.AppHost
```

Open the Aspire dashboard link printed in the console. When the `api` resource is healthy, the API is at
`http://localhost:5240`. Open it in a browser for the sample's home page: a button for the happy path and each
failure path, and a form for your own orders. Or follow that sample's README, or run the requests in its `.http`
file (`src/TicketTide.Api/TicketTide.Api.http`, or `src/TicketTide.Web/TicketTide.Web.http` for the monolith samples).

Every sample uses the same ports, so run one sample at a time. Stop it with Ctrl+C.

## Demonstrate the differences

A good order for a session:

1. **[Epic Saga](epic-saga/).** Buy tickets, then buy again with `simulateTicketingFailures: 1`. Show the
   compensations in the order's `history`, the refunded payment, and the released seats. This is the
   "distributed transaction" everyone expects.
2. **[Fairy Tale Saga](fairy-tale-saga/).** The same failure, but the fan gets `Pending` and the tickets arrive
   later. Nothing is undone. Ask: which is better for a fan during a big on-sale?
3. **[Phone Tag Saga](phone-tag-saga/)** and **[Time Travel Saga](time-travel-saga/).** The same two outcomes
   without an orchestrator. Look at how the workflow and the undo logic are spread across the modules.
4. **[Parallel Saga](parallel-saga/).** Asynchronous messages. Watch the queues in the RabbitMQ management UI,
   and the overlapping spans in the dashboard's **Traces** view. Then try the sold-out event: the card was
   charged in parallel, so it has to be refunded.
5. **[Anthology Saga](anthology-saga/).** No orchestrator and no waiting. Ticketing's message is simply
   retried until it succeeds. The lowest coupling, and the hardest to follow.
6. **[Fantasy Fiction Saga](fantasy-fiction-saga/)** and **[Horror Story Saga](horror-story-saga/).** What
   happens when you insist on atomicity with asynchronous messages: tracking every in-flight request, and
   compensations racing each other. These are the two combinations the book warns about.

In every sample, the dashboard's **Traces** view shows each request across the modules and their databases,
and Papercut shows every email the fan received.

## Build, test, and check formatting

From the repository root:

```sh
dotnet BuildTestFormat.cs                 # every sample: build, run the tests, check dotnet format
dotnet BuildTestFormat.cs epic-saga       # just one sample
dotnet BuildTestFormat.cs --no-test       # skip the tests (they need Docker)
```

Shared settings live in this folder: `Directory.Build.props`, `Directory.Packages.props` (central package
versions), `global.json` (the .NET SDK and the Microsoft.Testing.Platform test runner), `nuget.config`, and
`.editorconfig`.

## What the samples leave out

They are teaching samples, so they skip things a production system would need:

- **A transactional outbox.** Modules save their work, then publish. If the process dies in between, a
  message can be lost. Handlers are idempotent, so a message that is retried or delivered twice is safe.
- **Crash recovery for in-flight sagas.** Some samples resume pending work after a restart; none of them resume
  a saga that was halfway through a synchronous step.
- **Authentication, real payments, and migrations.** Everything is anonymous, the payment provider is fake,
  and the databases are created on startup with `EnsureCreated`. Order ids are time-ordered GUIDs, so with no
  authentication anyone could guess another fan's order. Do not copy that into production.
- **A clean public API.** `simulateTicketingFailures` is a demo switch in the public request. A real system would
  inject faults in test environments only. The payment token is a provider token, never card data, but the
  samples still store it with the order and pass it between modules in plain text.
