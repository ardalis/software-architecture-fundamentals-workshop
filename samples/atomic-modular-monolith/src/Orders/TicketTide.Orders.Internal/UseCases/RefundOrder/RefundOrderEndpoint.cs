using FastEndpoints;
using Mediator;
using TicketTide.SharedKernel.Web;

namespace TicketTide.Orders.Internal.UseCases.RefundOrder;

public sealed record RefundOrderRequest(Guid OrderId);

internal sealed class RefundOrderEndpoint(ISender sender) : Endpoint<RefundOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders/{orderId}/refund");
        AllowAnonymous();
        Summary(s => s.Summary = "Refund a paid order. Its tickets stop working and its seats go back on sale.");
    }

    public override async Task HandleAsync(RefundOrderRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new RefundOrderCommand(req.OrderId), ct);
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToFailureResponse());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
