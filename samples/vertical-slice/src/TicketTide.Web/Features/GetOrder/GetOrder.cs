using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TicketTide.Web.Data;

namespace TicketTide.Web.Features.GetOrder;

public sealed record GetOrderRequest(Guid OrderId);

internal sealed class GetOrderEndpoint(AppDbContext db) : Endpoint<GetOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Get("/orders/{orderId}");
        AllowAnonymous();
        Summary(s => s.Summary = "An order and its tickets.");
    }

    public override async Task HandleAsync(GetOrderRequest req, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Tickets)
            .FirstOrDefaultAsync(o => o.Id == req.OrderId, ct);
        if (order is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(OrderResponse.From(order), ct);
    }
}
