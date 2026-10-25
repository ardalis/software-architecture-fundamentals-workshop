using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TicketTide.Checkout.Contracts;
using TicketTide.Checkout.Data;
using TicketTide.Messaging;
using Order = TicketTide.Checkout.Data.Order;

namespace TicketTide.Checkout.Endpoints;

/// <summary>
/// Buy tickets. Checkout saves the order, publishes <see cref="OrderPlaced"/>, and answers right away with
/// <c>202 Accepted</c> and a <c>Pending</c> order ("Order Received"). It does not wait for anything.
/// Poll <c>GET /orders/{id}</c>, or wait for the email.
/// </summary>
internal sealed class PlaceOrderEndpoint(CheckoutDbContext db, IMessageBus bus) : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets. Starts the Anthology Saga and returns at once.");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        var order = new Order
        {
            EventId = req.EventId,
            Quantity = req.Quantity,
            FanEmail = req.FanEmail,
            PaymentToken = req.PaymentToken,
            SimulateTicketingFailures = req.SimulateTicketingFailures,
        };

        // Once the order is saved, its event must be published even if the fan closes the browser,
        // so neither step uses the request's cancellation token.
        db.Orders.Add(order);
        await db.SaveChangesAsync(CancellationToken.None);

        // Checkout does not know (or care) who reacts. Inventory happens to be listening.
        await bus.PublishAsync(
            new OrderPlaced(order.Id, order.EventId, order.Quantity, order.PaymentToken, order.SimulateTicketingFailures),
            CancellationToken.None);

        await Send.AcceptedAtAsync<GetOrderEndpoint>(new { orderId = order.Id }, OrderResponse.From(order), cancellation: ct);
    }
}

public sealed record GetOrderRequest(Guid OrderId);

internal sealed class GetOrderEndpoint(CheckoutDbContext db) : Endpoint<GetOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Get("/orders/{orderId}");
        AllowAnonymous();
        Summary(s => s.Summary = "An order, its saga status, and the history of every event Checkout heard.");
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
