using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Saga;
using Order = TicketTide.Checkout.Data.Order;

namespace TicketTide.Checkout.Endpoints;

/// <summary>
/// Buy tickets. The fan waits while the request travels down the chain and the replies travel back up:
/// <c>201 Created</c> when the tickets are confirmed, <c>202 Accepted</c> when the seats are held and the card is
/// charged but Ticketing is still being retried, or <c>422 Unprocessable Entity</c> when the purchase failed.
/// </summary>
internal sealed class PlaceOrderEndpoint(CheckoutService checkout) : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets. Runs the Time Travel Saga and returns the result.");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        var order = await checkout.PlaceOrderAsync(new Order
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

        if (order.Status is OrderStatus.Pending or OrderStatus.NeedsAttention)
        {
            // Not done yet, but nothing failed for good either. Poll GET /orders/{id}, or wait for the email.
            await Send.ResponseAsync(response, StatusCodes.Status202Accepted, ct);
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
