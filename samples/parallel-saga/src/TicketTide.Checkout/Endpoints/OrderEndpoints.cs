using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Saga;
using Order = TicketTide.Checkout.Data.Order;

namespace TicketTide.Checkout.Endpoints;

/// <summary>
/// Buy tickets. The orchestrator answers right away with <c>202 Accepted</c> and a <c>Pending</c> order,
/// then runs the saga in the background. Poll <c>GET /orders/{id}</c>, or wait for the email.
/// An unknown event is rejected at once with <c>422 Unprocessable Entity</c>.
/// </summary>
internal sealed class PlaceOrderEndpoint(CheckoutOrchestrator orchestrator) : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets. Starts the Parallel Saga and returns at once.");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        // Once the order is saved, its commands must be sent even if the fan closes the browser,
        // so the saga does not use the request's cancellation token.
        var order = await orchestrator.StartAsync(new Order
        {
            EventId = req.EventId,
            Quantity = req.Quantity,
            FanEmail = req.FanEmail,
            PaymentToken = req.PaymentToken,
            SimulateTicketingFailures = req.SimulateTicketingFailures,
        }, CancellationToken.None);

        var response = OrderResponse.From(order);
        if (order.Status == OrderStatus.Pending)
        {
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
