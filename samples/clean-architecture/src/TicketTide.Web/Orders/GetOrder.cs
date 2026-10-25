using FastEndpoints;
using TicketTide.Core.Interfaces;

namespace TicketTide.Web.Orders;

public sealed record GetOrderRequest(Guid OrderId);

internal sealed class GetOrder(IOrderRepository orders) : Endpoint<GetOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Get("/orders/{orderId}");
        AllowAnonymous();
        Summary(s => s.Summary = "An order and its tickets.");
    }

    public override async Task HandleAsync(GetOrderRequest req, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(OrderResponse.From(order), ct);
    }
}
