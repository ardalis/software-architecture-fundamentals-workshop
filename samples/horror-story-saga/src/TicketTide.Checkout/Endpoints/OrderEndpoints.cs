using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TicketTide.Checkout.Contracts;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Saga;
using TicketTide.Messaging;
using Order = TicketTide.Checkout.Data.Order;

namespace TicketTide.Checkout.Endpoints;

/// <summary>
/// Buy tickets. Checkout publishes Order Placed and the fan waits while the events travel along the chain:
/// <c>201 Created</c> when the tickets are confirmed, or <c>422 Unprocessable Entity</c> once every step has been
/// undone. If the saga is still running after <see cref="WaitTimeout"/>, the fan gets <c>202 Accepted</c> with the
/// order so far, and can poll <c>GET /orders/{id}</c> or wait for the email.
/// </summary>
internal sealed class PlaceOrderEndpoint(CheckoutDbContext db, IMessageBus bus, OrderCompletionSignal completion)
    : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets. Starts the Horror Story Saga and waits for the final result.");
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

        // Register before publishing, so the event that finishes the order cannot arrive before anyone listens.
        completion.Register(order.Id);

        // Once the order is saved, its event must be published even if the fan closes the browser,
        // so this part does not use the request's cancellation token.
        try
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync(CancellationToken.None);
            await bus.PublishAsync(
                new OrderPlaced(order.Id, order.EventId, order.Quantity, order.PaymentToken, order.SimulateTicketingFailures),
                CancellationToken.None);
        }
        catch
        {
            completion.Forget(order.Id); // Nobody will wait for this order now.
            throw;
        }

        // No one coordinates from here. Checkout just waits to hear how the story ends.
        await completion.WaitAsync(order.Id, WaitTimeout, ct);

        var latest = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id, ct);
        var response = OrderResponse.From(latest);
        switch (latest.Status)
        {
            case OrderStatus.Confirmed:
                await Send.CreatedAtAsync<GetOrderEndpoint>(new { orderId = latest.Id }, response, cancellation: ct);
                break;
            case OrderStatus.Failed:
                await Send.ResponseAsync(response, StatusCodes.Status422UnprocessableEntity, ct);
                break;
            default:
                await Send.AcceptedAtAsync<GetOrderEndpoint>(new { orderId = latest.Id }, response, cancellation: ct);
                break;
        }
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
