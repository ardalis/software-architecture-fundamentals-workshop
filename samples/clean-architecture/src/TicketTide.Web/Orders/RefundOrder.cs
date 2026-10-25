using FastEndpoints;
using TicketTide.Core.Services;

namespace TicketTide.Web.Orders;

public sealed record RefundOrderRequest(Guid OrderId);

internal sealed class RefundOrder(RefundOrderService service) : Endpoint<RefundOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders/{orderId}/refund");
        AllowAnonymous();
        Summary(s => s.Summary = "Refund a paid order. Its tickets stop working and its seats go back on sale.");
    }

    public override async Task HandleAsync(RefundOrderRequest req, CancellationToken ct)
    {
        var result = await service.RefundAsync(req.OrderId, ct);
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToFailureResponse());
            return;
        }

        await Send.OkAsync(OrderResponse.From(result.Value), ct);
    }
}
