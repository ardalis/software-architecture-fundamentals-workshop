using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;

namespace TicketTide.Inventory.Handlers;

public sealed class ReleaseSeatsHandler(InventoryDbContext db, ILogger<ReleaseSeatsHandler> logger)
    : ICommandHandler<ReleaseSeats>
{
    public async ValueTask<Unit> Handle(ReleaseSeats command, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var hold = await db.SeatHolds.FirstOrDefaultAsync(h => h.OrderId == command.OrderId, ct);
            if (hold is null || hold.Status == SeatHoldStatus.Released)
            {
                return; // Nothing to undo. Compensations must be idempotent.
            }

            hold.Status = SeatHoldStatus.Released;
            hold.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await db.Events
                .Where(e => e.Id == hold.EventId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsHeld, e => e.SeatsHeld - hold.Quantity), ct);

            await transaction.CommitAsync(ct);
            logger.LogInformation("Released {Quantity} seats for order {OrderId}", hold.Quantity, command.OrderId);
        }, cancellationToken);

        return Unit.Value;
    }
}
