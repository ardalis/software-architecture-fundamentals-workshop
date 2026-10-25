using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Messaging;

namespace TicketTide.Inventory.Handlers;

internal sealed class ReleaseSeatsHandler(InventoryDbContext db, IMessageBus bus, ILogger<ReleaseSeatsHandler> logger)
    : IMessageHandler<ReleaseSeats>
{
    public async Task HandleAsync(ReleaseSeats message, MessageContext context, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var hold = await db.SeatHolds.FirstOrDefaultAsync(h => h.OrderId == message.OrderId, ct);
            if (hold is null)
            {
                // The release arrived before the hold (messages can overtake each other).
                // Record a released hold, so a late HoldSeats for this order is rejected.
                db.SeatHolds.Add(new SeatHold
                {
                    OrderId = message.OrderId,
                    EventId = string.Empty,
                    Status = SeatHoldStatus.Released,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }

            if (hold.Status == SeatHoldStatus.Released)
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
            logger.LogInformation("Released {Quantity} seats for order {OrderId}", hold.Quantity, message.OrderId);
        }, cancellationToken);

        await bus.PublishAsync(new SeatsReleased(message.OrderId), cancellationToken);
    }
}
