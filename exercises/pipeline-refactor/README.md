# Exercise: Refactor Inline Concerns into the Pipeline

**Module:** Cross-Cutting Concerns and Processing Pipelines (2-day module 6)
**Format:** pairs, one laptop
**Time:** 25 min + 10 min debrief

## Goal

Move cross-cutting concerns out of endpoint code and into the ASP.NET Core pipeline, then weigh what you gained against what you hid.

## Materials

[`app.cs`](./app.cs) is a TicketTide orders API written as a .NET 10 file-based app. Every endpoint does its own timing, logging, validation, and error handling.

```sh
cd exercises/pipeline-refactor
dotnet run app.cs
```

In another terminal (the port is printed at startup, usually `http://localhost:5000`):

```sh
# Valid order (201)
curl -i -H "Content-Type: application/json" -d '{"email":"fan@example.com","eventCode":"ARENA-1","quantity":2}' http://localhost:5000/orders
# Invalid email (400)
curl -i -H "Content-Type: application/json" -d '{"email":"nope","eventCode":"ARENA-1","quantity":2}' http://localhost:5000/orders
# Simulated server failure (500)
curl -i -H "Content-Type: application/json" -d '{"email":"a@b.c","eventCode":"BOOM","quantity":2}' http://localhost:5000/orders
# Transfer (200), then transfer a missing order (404)
curl -i -H "Content-Type: application/json" -d '{"toEmail":"friend@example.com"}' http://localhost:5000/orders/1/transfer
curl -i -H "Content-Type: application/json" -d '{"toEmail":"friend@example.com"}' http://localhost:5000/orders/99/transfer
```

## Steps

1. **Run it and try the requests (3 min).** Note the status codes so you can confirm you haven't changed behavior.
2. **Pick one concern and move it (15 min).** Choose at least one:
   - **Timing and logging** into middleware (`app.Use(...)`)
   - **Validation** into an endpoint filter (`IEndpointFilter`, applied with `AddEndpointFilter`)
   - **Error handling** into `UseExceptionHandler` with an `IExceptionHandler` for `KeyNotFoundException`
3. **Re-run the requests (2 min).** Status codes should match. Log messages may change. Is that acceptable?
4. **Add a new endpoint (5 min).** Add `POST /orders/{id}/cancel`. How much cross-cutting code did you need to write this time?

## Debrief questions

- How many lines did each endpoint lose? How many did the pipeline gain?
- A new developer reads only the endpoint code. What behavior can they no longer see?
- Does order matter? What happens if logging runs before or after the exception handler?
- Which concerns are better left inline? (Hint: business-rule validation versus input-shape validation.)
- When would you reach for decorators or mediator behaviors instead of middleware and filters?
