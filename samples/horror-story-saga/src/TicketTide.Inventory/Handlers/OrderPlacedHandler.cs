using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Contracts;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Messaging;

namespace TicketTide.Inventory.Handlers;

/// <summary>
/// An order was placed: hold the seats, then publish Seats Held for Payments, with the amount to charge.
/// If publishing fails, the message is redelivered and this handler runs again; it finds the hold it already
/// made and just publishes the event again.
/// </summary>
internal sealed class OrderPlacedHandler(InventoryDbContext db, IMessageBus bus, ILogger<OrderPlacedHandler> logger)
    : IMessageHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced message, MessageContext context, CancellationToken cancellationToken)
    {
        var @event = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == message.EventId, cancellationToken);
        if (@event is null)
        {
            await bus.PublishAsync(new SeatsRejected(message.OrderId, $"Event '{message.EventId}' does not exist."), cancellationToken);
            return;
        }

        var existing = await db.SeatHolds.AsNoTracking().FirstOrDefaultAsync(h => h.OrderId == message.OrderId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == SeatHoldStatus.Held)
            {
                await bus.PublishAsync(SeatsHeldFor(message, @event, existing.Amount), cancellationToken);
            }

            return; // Released: the saga has already moved on, and must not start again.
        }

        // The seat count and the hold are written in one local transaction.
        // The conditional UPDATE stops two fans from taking the last seats at the same time.
        var amount = @event.Price * message.Quantity;
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
                Amount = amount,
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
        await bus.PublishAsync(SeatsHeldFor(message, @event, amount), cancellationToken);
    }

    // Inventory knows the price, so it works out the amount. The rest rides along for Payments and Ticketing.
    private static SeatsHeld SeatsHeldFor(OrderPlaced message, Event @event, decimal amount) => new(
        message.OrderId,
        message.EventId,
        @event.Name,
        message.Quantity,
        amount,
        message.PaymentToken,
        message.SimulateTicketingFailures);
}
