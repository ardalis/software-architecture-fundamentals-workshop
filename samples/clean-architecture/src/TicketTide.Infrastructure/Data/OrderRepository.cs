using Microsoft.EntityFrameworkCore;
using TicketTide.Core.Interfaces;
using TicketTide.Core.OrderAggregate;

namespace TicketTide.Infrastructure.Data;

internal sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    // Always load the whole aggregate: an order without its tickets can't enforce its rules.
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.Include(o => o.Tickets).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void Add(Order order) => db.Orders.Add(order);
}
