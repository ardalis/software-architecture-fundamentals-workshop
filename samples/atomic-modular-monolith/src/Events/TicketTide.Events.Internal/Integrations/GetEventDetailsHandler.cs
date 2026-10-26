using Ardalis.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.Events.Contracts;
using TicketTide.Events.Internal.Data;

namespace TicketTide.Events.Internal.Integrations;

public sealed class GetEventDetailsHandler(EventsDbContext db) : IQueryHandler<GetEventDetails, Result<EventDetails>>
{
    public async ValueTask<Result<EventDetails>> Handle(GetEventDetails query, CancellationToken cancellationToken)
    {
        var details = await db.Events.AsNoTracking()
            .Where(e => e.Id == query.EventId)
            .Select(e => new EventDetails(e.Id, e.Name, e.Price, e.Capacity - e.SeatsSold))
            .FirstOrDefaultAsync(cancellationToken);

        return details is null ? Result.NotFound($"There is no event with id {query.EventId}.") : details;
    }
}
