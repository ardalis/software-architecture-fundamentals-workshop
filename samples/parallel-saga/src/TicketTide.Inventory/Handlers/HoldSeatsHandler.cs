using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Messaging;

namespace TicketTide.Inventory.Handlers;

/// <summary>
/// Holds seats, then publishes the reply. If publishing fails, the message is redelivered and this handler
/// runs again; it finds the hold it already made and just publishes the reply again.
/// </summary>
internal sealed class HoldSeatsHandler(InventoryDbContext db, IMessageBus bus, ILogger<HoldSeatsHandler> logger)
    : IMessageHandler<HoldSeats>
{
    public async Task HandleAsync(HoldSeats message, MessageContext context, CancellationToken cancellationToken)
    {
        var existing = await db.SeatHolds.AsNoTracking().FirstOrDefaultAsync(h => h.OrderId == message.OrderId, cancellationToken);
        if (existing is not null)
        {
            // Already handled, or released by a compensation that arrived first.
            await (existing.Status == SeatHoldStatus.Held
                ? bus.PublishAsync(new SeatsHeld(message.OrderId), cancellationToken)
                : bus.PublishAsync(new SeatsRejected(message.OrderId, "The order was cancelled."), cancellationToken));
            return;
        }

        var @event = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == message.EventId, cancellationToken);
        if (@event is null)
        {
            await bus.PublishAsync(new SeatsRejected(message.OrderId, $"Event '{message.EventId}' does not exist."), cancellationToken);
            return;
        }

        // The seat count and the hold are written in one local transaction.
        // The conditional UPDATE stops two fans from taking the last seats at the same time.
        var strategy = db.Database.CreateExecutionStrategy();
        var held = await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var updated = await db.Events
                .Where(e => e.Id == message.EventId && e.Capacity - e.SeatsHeld >= message.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsHeld, e => e.SeatsHeld + message.Quantity), ct);
            if (updated == 0)
            {
                return false;
            }

            db.SeatHolds.Add(new SeatHold
            {
                OrderId = message.OrderId,
                EventId = message.EventId,
                Quantity = message.Quantity,
                Amount = @event.Price * message.Quantity,
                Status = SeatHoldStatus.Held,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }, cancellationToken);

        if (!held)
        {
            logger.LogInformation("Not enough seats left for order {OrderId}", message.OrderId);
            await bus.PublishAsync(new SeatsRejected(message.OrderId, $"Not enough seats left for {@event.Name}."), cancellationToken);
            return;
        }

        logger.LogInformation("Held {Quantity} seats for order {OrderId}", message.Quantity, message.OrderId);
        await bus.PublishAsync(new SeatsHeld(message.OrderId), cancellationToken);
    }
}
