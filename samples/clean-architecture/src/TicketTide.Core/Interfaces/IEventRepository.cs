using TicketTide.Core.EventAggregate;

namespace TicketTide.Core.Interfaces;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Event>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Puts seats back on sale right away, as one atomic change (<see cref="Event.ReturnSeats"/>), so it can't
    /// conflict with fans buying seats for the same event at the same moment.
    /// </summary>
    Task ReturnSeatsAsync(string eventId, int quantity, CancellationToken cancellationToken);
}
