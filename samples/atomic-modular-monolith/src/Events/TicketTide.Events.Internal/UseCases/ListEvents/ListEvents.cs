using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.Events.Internal.Data;

namespace TicketTide.Events.Internal.UseCases.ListEvents;

public sealed record EventResponse(
    string Id, string Name, string Venue, DateTimeOffset StartsAt, decimal Price, int Capacity, int SeatsAvailable);

/// <summary>The events on sale. Internal to the module: only its own endpoint sends it.</summary>
public sealed record ListEventsQuery : IQuery<List<EventResponse>>;

public sealed class ListEventsHandler(EventsDbContext db) : IQueryHandler<ListEventsQuery, List<EventResponse>>
{
    public async ValueTask<List<EventResponse>> Handle(ListEventsQuery query, CancellationToken cancellationToken)
    {
        var events = await db.Events.AsNoTracking()
            .Select(e => new EventResponse(e.Id, e.Name, e.Venue, e.StartsAt, e.Price, e.Capacity, e.Capacity - e.SeatsSold))
            .ToListAsync(cancellationToken);

        // Sorted here rather than in SQL, so the module's tests can use SQLite, which can't sort by DateTimeOffset.
        return [.. events.OrderBy(e => e.StartsAt)];
    }
}
