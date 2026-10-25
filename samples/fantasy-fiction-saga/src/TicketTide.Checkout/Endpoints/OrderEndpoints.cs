using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Saga;
using Order = TicketTide.Checkout.Data.Order;

namespace TicketTide.Checkout.Endpoints;

/// <summary>
/// Buy tickets. The saga runs over RabbitMQ, but the fan still waits for the final answer:
/// <c>201 Created</c> when the tickets are confirmed, or <c>422 Unprocessable Entity</c> when the purchase failed
/// and everything was undone. If the saga is still running after <see cref="WaitTimeout"/>, the fan gets
/// <c>202 Accepted</c> and the order as it is now, and can poll <c>GET /orders/{id}</c>.
/// </summary>
internal sealed class PlaceOrderEndpoint(
    CheckoutOrchestrator orchestrator,
    OrderCompletionSignal completion,
    CheckoutDbContext db) : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets. Runs the Fantasy Fiction Saga and waits for the final result.");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        // Once the order is saved, its commands must be sent even if the fan closes the browser,
        // so the saga does not use the request's cancellation token.
        var order = new Order
        {
            EventId = req.EventId,
            Quantity = req.Quantity,
            FanEmail = req.FanEmail,
            PaymentToken = req.PaymentToken,
            SimulateTicketingFailures = req.SimulateTicketingFailures,
        };

        try
        {
            order = await orchestrator.StartAsync(order, CancellationToken.None);
        }
        catch
        {
            completion.Forget(order.Id); // Nobody will wait for this order now.
            throw;
        }

        if (!order.IsFinished)
        {
            // The replies are handled on the checkout queue. Wait here until the orchestrator says the order is done,
            // then read the final state from the database.
            await completion.WaitAsync(order.Id, WaitTimeout, ct);
            order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id, ct);
        }

        var response = OrderResponse.From(order);
        switch (order.Status)
        {
            case OrderStatus.Confirmed:
                await Send.CreatedAtAsync<GetOrderEndpoint>(new { orderId = order.Id }, response, cancellation: ct);
                break;
            case OrderStatus.Failed:
                await Send.ResponseAsync(response, StatusCodes.Status422UnprocessableEntity, ct);
                break;
            default:
                // Still Pending or Compensating. Be honest about it rather than guess.
                await Send.AcceptedAtAsync<GetOrderEndpoint>(new { orderId = order.Id }, response, cancellation: ct);
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
