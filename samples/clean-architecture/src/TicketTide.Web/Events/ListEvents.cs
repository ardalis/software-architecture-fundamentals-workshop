using FastEndpoints;
using TicketTide.Core.Interfaces;

namespace TicketTide.Web.Events;

public sealed record EventResponse(
    string Id, string Name, string Venue, DateTimeOffset StartsAt, decimal Price, int Capacity, int SeatsAvailable);

internal sealed class ListEvents(IEventRepository events) : EndpointWithoutRequest<List<EventResponse>>
{
    public override void Configure()
    {
        Get("/events");
        AllowAnonymous();
        Summary(s => s.Summary = "The events on sale, and how many seats each has left.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var all = await events.ListAsync(ct);
        await Send.OkAsync(
            [.. all.Select(e => new EventResponse(e.Id, e.Name, e.Venue, e.StartsAt, e.Price, e.Capacity, e.SeatsAvailable))],
            ct);
    }
}
