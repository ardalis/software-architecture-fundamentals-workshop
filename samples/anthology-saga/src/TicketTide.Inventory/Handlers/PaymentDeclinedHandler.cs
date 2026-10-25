using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;

namespace TicketTide.Inventory.Handlers;

/// <summary>
/// A declined card is a business "no", not a technical failure, so retrying will not help.
/// Inventory compensates by releasing the seats it held, then publishes <see cref="SeatsReleased"/>.
/// </summary>
internal sealed class PaymentDeclinedHandler(InventoryDbContext db, IMessageBus bus, ILogger<PaymentDeclinedHandler> logger)
    : IMessageHandler<PaymentDeclined>
{
    public async Task HandleAsync(PaymentDeclined message, MessageContext context, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        var released = await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Payments only reacts to Seats Held, so the hold should always be there.
            var hold = await db.SeatHolds.FirstOrDefaultAsync(h => h.OrderId == message.OrderId, ct);
            if (hold is null)
            {
                return false;
            }

            if (hold.Status == SeatHoldStatus.Released)
            {
                return true; // Redelivered: nothing left to undo, so just publish again. Compensations must be idempotent.
            }

            hold.Status = SeatHoldStatus.Released;
            hold.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await db.Events
                .Where(e => e.Id == hold.EventId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsHeld, e => e.SeatsHeld - hold.Quantity), ct);

            await transaction.CommitAsync(ct);
            logger.LogInformation("Released {Quantity} seats for order {OrderId}", hold.Quantity, message.OrderId);
            return true;
        }, cancellationToken);

        if (!released)
        {
            logger.LogWarning("Payment declined for order {OrderId}, but Inventory holds no seats for it", message.OrderId);
            return;
        }

        await bus.PublishAsync(new SeatsReleased(message.OrderId), cancellationToken);
    }
}
