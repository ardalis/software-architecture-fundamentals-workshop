using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TicketTide.Web.Data;

namespace TicketTide.Web.Features.ListEvents;

public sealed record EventResponse(
    string Id, string Name, string Venue, DateTimeOffset StartsAt, decimal Price, int Capacity, int SeatsAvailable);

internal sealed class ListEventsEndpoint(AppDbContext db) : EndpointWithoutRequest<List<EventResponse>>
{
    public override void Configure()
    {
        Get("/events");
        AllowAnonymous();
        Summary(s => s.Summary = "The events on sale, and how many seats each has left.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // A read slice can query exactly what it needs and project straight to the response.
        var events = await db.Events.AsNoTracking()
            .OrderBy(e => e.StartsAt)
            .Select(e => new EventResponse(e.Id, e.Name, e.Venue, e.StartsAt, e.Price, e.Capacity, e.Capacity - e.SeatsSold))
            .ToListAsync(ct);

        await Send.OkAsync(events, ct);
    }
}
