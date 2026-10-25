using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Contracts;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Messaging;

namespace TicketTide.Inventory.Handlers;

/// <summary>
/// When an order is placed, hold the seats and price the order, then publish <see cref="SeatsHeld"/>
/// (or <see cref="SeatsRejected"/>). Nobody told Inventory to do this; it reacts to Checkout's event.
/// If publishing fails, the message is redelivered and this handler runs again; it finds the hold it
/// already made and just publishes the event again.
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
            // Already handled. If the seats were released since, the saga has moved on and there is nothing to say.
            if (existing.Status == SeatHoldStatus.Held)
            {
                await bus.PublishAsync(SeatsHeldFor(message, @event, existing.Amount), cancellationToken);
            }

            return;
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

    // Pass on what the next services need: the payment token for Payments, the failure count for Ticketing.
    private static SeatsHeld SeatsHeldFor(OrderPlaced message, Event @event, decimal amount) => new(
        message.OrderId,
        message.EventId,
        @event.Name,
        message.Quantity,
        amount,
        message.PaymentToken,
        message.SimulateTicketingFailures);
}
