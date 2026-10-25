using Microsoft.EntityFrameworkCore;
using TicketTide.Core.EventAggregate;
using TicketTide.Core.Interfaces;

namespace TicketTide.Infrastructure.Data;

internal sealed class EventRepository(AppDbContext db) : IEventRepository
{
    public Task<Event?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        db.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Event>> ListAsync(CancellationToken cancellationToken) =>
        await db.Events.AsNoTracking().OrderBy(e => e.StartsAt).ToListAsync(cancellationToken);

    // Event.ReturnSeats as one UPDATE statement, so it needs no concurrency check.
    public Task ReturnSeatsAsync(string eventId, int quantity, CancellationToken cancellationToken) =>
        db.Events
            .Where(e => e.Id == eventId && e.SeatsSold >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsSold, e => e.SeatsSold - quantity), cancellationToken);
}
