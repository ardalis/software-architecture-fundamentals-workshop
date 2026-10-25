using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TicketTide.Inventory.Data;

namespace TicketTide.Inventory.Endpoints;

public sealed record EventResponse(
    string Id, string Name, string Venue, DateTimeOffset StartsAt, decimal Price, int Capacity, int SeatsAvailable);

internal sealed class ListEventsEndpoint(InventoryDbContext db) : EndpointWithoutRequest<List<EventResponse>>
{
    public override void Configure()
    {
        Get("/inventory/events");
        AllowAnonymous();
        Summary(s => s.Summary = "Events on sale and how many seats are left.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var events = await db.Events.AsNoTracking()
            .OrderBy(e => e.StartsAt)
            .Select(e => new EventResponse(e.Id, e.Name, e.Venue, e.StartsAt, e.Price, e.Capacity, e.Capacity - e.SeatsHeld))
            .ToListAsync(ct);

        await Send.OkAsync(events, ct);
    }
}

public sealed record SeatHoldRequest(Guid OrderId);

public sealed record SeatHoldResponse(Guid OrderId, string EventId, int Quantity, decimal Amount, string Status);

internal sealed class GetSeatHoldEndpoint(InventoryDbContext db) : Endpoint<SeatHoldRequest, SeatHoldResponse>
{
    public override void Configure()
    {
        Get("/inventory/holds/{orderId}");
        AllowAnonymous();
        Summary(s => s.Summary = "The seat hold for an order: Held or Released.");
    }

    public override async Task HandleAsync(SeatHoldRequest req, CancellationToken ct)
    {
        var hold = await db.SeatHolds.AsNoTracking().FirstOrDefaultAsync(h => h.OrderId == req.OrderId, ct);
        if (hold is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new SeatHoldResponse(hold.OrderId, hold.EventId, hold.Quantity, hold.Amount, hold.Status.ToString()), ct);
    }
}
