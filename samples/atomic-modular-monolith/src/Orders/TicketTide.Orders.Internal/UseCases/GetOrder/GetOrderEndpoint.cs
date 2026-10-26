using FastEndpoints;
using Mediator;
using TicketTide.SharedKernel.Web;

namespace TicketTide.Orders.Internal.UseCases.GetOrder;

public sealed record GetOrderRequest(Guid OrderId);

internal sealed class GetOrderEndpoint(ISender sender) : Endpoint<GetOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Get("/orders/{orderId}");
        AllowAnonymous();
        Summary(s => s.Summary = "An order and its tickets.");
    }

    public override async Task HandleAsync(GetOrderRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetOrderQuery(req.OrderId), ct);
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToFailureResponse());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
