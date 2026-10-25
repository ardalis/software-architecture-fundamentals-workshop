using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;

namespace TicketTide.Inventory.Handlers;

public sealed class GetEventPriceHandler(InventoryDbContext db) : IQueryHandler<GetEventPrice, EventPrice?>
{
    public async ValueTask<EventPrice?> Handle(GetEventPrice query, CancellationToken cancellationToken) =>
        await db.Events.AsNoTracking()
            .Where(e => e.Id == query.EventId)
            .Select(e => new EventPrice(e.Id, e.Name, e.Price))
            .FirstOrDefaultAsync(cancellationToken);
}
