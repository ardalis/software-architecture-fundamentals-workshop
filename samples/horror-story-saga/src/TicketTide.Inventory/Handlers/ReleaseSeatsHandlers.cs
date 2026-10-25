using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Inventory.Handlers;

/// <summary>
/// Inventory's compensation. Something downstream failed, so give the seats back.
/// <para>
/// With no orchestrator, Inventory must work out for itself when the compensation is complete. Seats Released
/// is the last compensation, and Checkout tells the fan as soon as it hears it, so Inventory may only publish it
/// once every service downstream has finished undoing its step:
/// </para>
/// <list type="bullet">
/// <item>Card declined: nothing was charged and Ticketing never ran, so Inventory can report at once.</item>
/// <item>Ticketing failed: Payments is refunding the card at the same time. Inventory must hear both
/// Ticket Issue Failed and Card Refunded, and they can arrive in either order.</item>
/// </list>
/// Each event is written down on the seat hold as it arrives, so the order of arrival does not matter.
/// Seats Released lists them in that order, so the race shows up in the order's history.
/// </summary>
internal sealed class ReleaseSeatsHandlers(InventoryDbContext db, IMessageBus bus, ILogger<ReleaseSeatsHandlers> logger) :
    IMessageHandler<PaymentDeclined>,
    IMessageHandler<TicketIssueFailed>,
    IMessageHandler<CardRefunded>
{
    public Task HandleAsync(PaymentDeclined message, MessageContext context, CancellationToken ct) =>
        HearAsync(message.OrderId, nameof(PaymentDeclined), $"Payments could not charge the card: {message.Reason}", ct);

    public Task HandleAsync(TicketIssueFailed message, MessageContext context, CancellationToken ct) =>
        HearAsync(message.OrderId, nameof(TicketIssueFailed), $"Ticketing could not issue the tickets: {message.Reason}", ct);

    // The refund may arrive first. It still means the order is over, so release the seats now,
    // but wait for Ticketing's failure before reporting.
    public Task HandleAsync(CardRefunded message, MessageContext context, CancellationToken ct) =>
        HearAsync(message.OrderId, nameof(CardRefunded), reason: null, ct);

    private async Task HearAsync(Guid orderId, string heard, string? reason, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        var hold = await strategy.ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear(); // A retried attempt must start from what is in the database.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var hold = await db.SeatHolds.FirstOrDefaultAsync(h => h.OrderId == orderId, ct);
            if (hold is null)
            {
                return null;
            }

            if (!hold.Heard.Contains(heard))
            {
                hold.Heard.Add(heard);
            }

            hold.ReleaseReason = reason ?? hold.ReleaseReason;
            var releasing = hold.Status == SeatHoldStatus.Held;
            hold.Status = SeatHoldStatus.Released;
            hold.UpdatedAt = DateTimeOffset.UtcNow;

            // If another event changed this hold at the same moment, this throws, the message is redelivered,
            // and the next attempt sees both events.
            await db.SaveChangesAsync(ct);

            // Only the first event to arrive gives the seats back. Compensations must be idempotent.
            if (releasing)
            {
                await db.Events
                    .Where(e => e.Id == hold.EventId)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsHeld, e => e.SeatsHeld - hold.Quantity), ct);
                logger.LogInformation("Released {Quantity} seats for order {OrderId}", hold.Quantity, orderId);
            }

            await transaction.CommitAsync(ct);
            return hold;
        }, cancellationToken);

        if (hold is null)
        {
            // Every failure event is caused by Seats Held, so the hold is always there first.
            logger.LogWarning("No seat hold for order {OrderId}; nothing to release", orderId);
            return;
        }

        if (!hold.EveryCompensationIsIn)
        {
            logger.LogInformation("Seats for order {OrderId} are released; waiting for the other compensations", orderId);
            return;
        }

        // Published again if this event is redelivered. Checkout ignores the duplicate.
        await bus.PublishAsync(new SeatsReleased(orderId, hold.ReleaseReason ?? "The order was cancelled.", hold.Heard), cancellationToken);
    }
}
