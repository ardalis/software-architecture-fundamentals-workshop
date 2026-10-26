using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.Tickets.Contracts;
using TicketTide.Tickets.Internal.Data;

namespace TicketTide.Tickets.Internal.Integrations;

public sealed class GetTicketsForOrderHandler(TicketsDbContext db) : IQueryHandler<GetTicketsForOrder, IReadOnlyList<TicketDetails>>
{
    public async ValueTask<IReadOnlyList<TicketDetails>> Handle(GetTicketsForOrder query, CancellationToken cancellationToken)
    {
        var tickets = await db.Tickets.AsNoTracking()
            .Where(t => t.OrderId == query.OrderId)
            .OrderBy(t => t.Number)
            .ToListAsync(cancellationToken);
        return tickets.Select(t => t.ToDetails()).ToList();
    }
}
