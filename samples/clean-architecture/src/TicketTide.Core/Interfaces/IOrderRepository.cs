using TicketTide.Core.OrderAggregate;

namespace TicketTide.Core.Interfaces;

public interface IOrderRepository
{
    /// <summary>The order with its tickets, or null if there isn't one.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Order order);
}
