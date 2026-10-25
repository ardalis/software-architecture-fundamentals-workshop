using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;

namespace TicketTide.Inventory.Handlers;

public sealed class HoldSeatsHandler(InventoryDbContext db, ILogger<HoldSeatsHandler> logger)
    : ICommandHandler<HoldSeats, HoldSeatsResult>
{
    public async ValueTask<HoldSeatsResult> Handle(HoldSeats command, CancellationToken cancellationToken)
    {
        var existing = await db.SeatHolds.AsNoTracking()
            .FirstOrDefaultAsync(h => h.OrderId == command.OrderId, cancellationToken);
        if (existing is not null)
        {
            var name = await db.Events.Where(e => e.Id == existing.EventId).Select(e => e.Name).FirstAsync(cancellationToken);
            return existing.Status == SeatHoldStatus.Held
                ? HoldSeatsResult.Held(name, existing.Amount)
                : HoldSeatsResult.Rejected("The seats for this order were already released.");
        }

        var @event = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken);
        if (@event is null)
        {
            return HoldSeatsResult.Rejected($"Event '{command.EventId}' does not exist.");
        }

        // The seat count and the hold are written in one local transaction.
        // The conditional UPDATE stops two fans from taking the last seats at the same time.
        var strategy = db.Database.CreateExecutionStrategy();
        var held = await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var updated = await db.Events
                .Where(e => e.Id == command.EventId && e.Capacity - e.SeatsHeld >= command.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsHeld, e => e.SeatsHeld + command.Quantity), ct);
            if (updated == 0)
            {
                return false;
            }

            db.SeatHolds.Add(new SeatHold
            {
                OrderId = command.OrderId,
                EventId = command.EventId,
                Quantity = command.Quantity,
                Amount = @event.Price * command.Quantity,
                Status = SeatHoldStatus.Held,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }, cancellationToken);

        if (!held)
        {
            logger.LogInformation("Not enough seats left for order {OrderId}", command.OrderId);
            return HoldSeatsResult.Rejected($"Not enough seats left for {@event.Name}.");
        }

        logger.LogInformation("Held {Quantity} seats for order {OrderId}", command.Quantity, command.OrderId);
        return HoldSeatsResult.Held(@event.Name, @event.Price * command.Quantity);
    }
}
