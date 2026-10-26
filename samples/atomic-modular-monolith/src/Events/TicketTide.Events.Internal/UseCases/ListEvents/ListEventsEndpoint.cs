using FastEndpoints;
using Mediator;

namespace TicketTide.Events.Internal.UseCases.ListEvents;

internal sealed class ListEventsEndpoint(ISender sender) : EndpointWithoutRequest<List<EventResponse>>
{
    public override void Configure()
    {
        Get("/events");
        AllowAnonymous();
        Summary(s => s.Summary = "The events on sale, and how many seats each has left.");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(await sender.Send(new ListEventsQuery(), ct), ct);
}
