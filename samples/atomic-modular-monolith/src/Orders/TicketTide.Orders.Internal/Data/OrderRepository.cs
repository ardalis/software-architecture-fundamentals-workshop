using Ardalis.Result;
using Microsoft.EntityFrameworkCore;

namespace TicketTide.Orders.Internal.Data;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Order order);

    /// <summary>
    /// Saves the module's changes. Inside a unit of work they're only written for now: they become permanent when
    /// the transaction commits, and disappear if it rolls back.
    /// </summary>
    /// <returns>Conflict if someone else changed the same order first, or Error if the database rejected it.</returns>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed class OrderRepository(OrdersDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void Add(Order order) => db.Orders.Add(order);

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict();
        }
        catch (DbUpdateException ex)
        {
            return Result.Error(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
