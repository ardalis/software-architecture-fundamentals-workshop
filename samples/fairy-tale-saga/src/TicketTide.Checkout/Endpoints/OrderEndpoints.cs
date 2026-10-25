using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Saga;
using Order = TicketTide.Checkout.Data.Order;

namespace TicketTide.Checkout.Endpoints;

/// <summary>
/// Buy tickets. The fan waits while the saga runs and gets an honest answer:
/// <c>201 Created</c> when the tickets are confirmed, <c>202 Accepted</c> when the order is in but Ticketing failed
/// (the order is pending and the orchestrator keeps retrying), or <c>422 Unprocessable Entity</c> when the purchase
/// was rejected (sold out, card declined) and the held seats were released.
/// </summary>
internal sealed class PlaceOrderEndpoint(CheckoutOrchestrator orchestrator) : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets. Runs the Fairy Tale Saga and returns the result, which may be pending.");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        var order = await orchestrator.PlaceOrderAsync(new Order
        {
            EventId = req.EventId,
            Quantity = req.Quantity,
            FanEmail = req.FanEmail,
            PaymentToken = req.PaymentToken,
            SimulateTicketingFailures = req.SimulateTicketingFailures,
        });

        var response = OrderResponse.From(order);
        if (order.Status == OrderStatus.Confirmed)
        {
            await Send.CreatedAtAsync<GetOrderEndpoint>(new { orderId = order.Id }, response, cancellation: ct);
            return;
        }

        if (order.Status == OrderStatus.Pending)
        {
            // The order is in and paid for; the tickets are on the way. Follow it at the Location header.
            await Send.AcceptedAtAsync<GetOrderEndpoint>(new { orderId = order.Id }, response, cancellation: ct);
            return;
        }

        await Send.ResponseAsync(response, StatusCodes.Status422UnprocessableEntity, ct);
    }
}

public sealed record GetOrderRequest(Guid OrderId);

internal sealed class GetOrderEndpoint(CheckoutDbContext db) : Endpoint<GetOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Get("/orders/{orderId}");
        AllowAnonymous();
        Summary(s => s.Summary = "An order, its saga status, and the history of every step.");
    }

    public override async Task HandleAsync(GetOrderRequest req, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == req.OrderId, ct);
        if (order is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(OrderResponse.From(order), ct);
    }
}
