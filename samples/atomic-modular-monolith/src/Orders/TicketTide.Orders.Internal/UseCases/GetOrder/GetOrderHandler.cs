using Ardalis.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.Orders.Internal.Data;
using TicketTide.Tickets.Contracts;

namespace TicketTide.Orders.Internal.UseCases.GetOrder;

/// <summary>An order and its tickets. Internal to the module: only its own endpoint sends it.</summary>
public sealed record GetOrderQuery(Guid OrderId) : IQuery<Result<OrderResponse>>;

public sealed class GetOrderHandler(OrdersDbContext db, ISender sender) : IQueryHandler<GetOrderQuery, Result<OrderResponse>>
{
    public async ValueTask<Result<OrderResponse>> Handle(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == query.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.NotFound();
        }

        // The tickets belong to the Tickets module, so ask it for them through its Contracts.
        var tickets = await sender.Send(new GetTicketsForOrder(order.Id), cancellationToken);
        return OrderResponse.From(order, tickets);
    }
}
