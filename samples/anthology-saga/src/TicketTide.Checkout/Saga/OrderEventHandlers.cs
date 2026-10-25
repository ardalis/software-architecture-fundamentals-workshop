using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Notifications;
using TicketTide.Inventory.Contracts;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// Anthology Saga: asynchronous, eventually consistent, choreographed.
/// There is no orchestrator. Checkout published Order Placed and answered the fan; from then on it only listens.
/// Each event the other modules publish lands on the checkout queue, and Checkout records it in the order's history.
/// Tickets Issued confirms the order. A business "no" (sold out, card declined) fails it.
/// Checkout never hears about Ticketing failures: those are retried inside Ticketing by redelivery.
/// </summary>
internal sealed class OrderEventHandlers(
    CheckoutDbContext db,
    IFanNotifier notifier,
    ILogger<OrderEventHandlers> logger) :
    IMessageHandler<SeatsHeld>,
    IMessageHandler<SeatsRejected>,
    IMessageHandler<SeatsReleased>,
    IMessageHandler<PaymentReceived>,
    IMessageHandler<PaymentDeclined>,
    IMessageHandler<TicketsIssued>
{
    public Task HandleAsync(SeatsHeld message, MessageContext context, CancellationToken ct) =>
        RecordAsync(message.OrderId, $"Inventory held {message.Quantity} seat(s) for {message.EventName}.", order =>
        {
            // Inventory priced the order, so this is the first time Checkout learns the amount.
            order.EventName = message.EventName;
            order.Amount = message.Amount;
        }, ct);

    public Task HandleAsync(PaymentReceived message, MessageContext context, CancellationToken ct) =>
        RecordAsync(message.OrderId, $"Payments charged {Money.Format(message.Amount)}.", apply: null, ct);

    public Task HandleAsync(SeatsReleased message, MessageContext context, CancellationToken ct) =>
        RecordAsync(message.OrderId, "Inventory released the seats.", apply: null, ct);

    public Task HandleAsync(SeatsRejected message, MessageContext context, CancellationToken ct) =>
        FailAsync(message.OrderId, $"Inventory could not hold the seats: {message.Reason}", ct);

    public Task HandleAsync(PaymentDeclined message, MessageContext context, CancellationToken ct) =>
        FailAsync(message.OrderId, $"Payments could not charge the card: {message.Reason}", ct);

    public async Task HandleAsync(TicketsIssued message, MessageContext context, CancellationToken ct)
    {
        var order = await FindAsync(message.OrderId, ct);
        if (order.IsFinished)
        {
            return; // The same event again. The fan already has the tickets.
        }

        if (order.Amount is null)
        {
            // Events can arrive out of order, for example when Seats Held was redelivered. Throwing puts
            // this message back on the queue, so it is handled again after Seats Held.
            throw new InvalidOperationException($"Order {order.Id}: Tickets Issued arrived before Seats Held.");
        }

        order.TicketCodes = [.. message.TicketCodes];
        order.Note($"Ticketing issued {message.TicketCodes.Count} ticket(s).");
        order.Confirm();
        await db.SaveChangesAsync(ct); // A concurrency conflict throws here, and the event is redelivered.

        await NotifyFanAsync(order, () => notifier.SendConfirmationAsync(order, ct));
    }

    private async Task RecordAsync(Guid orderId, string note, Action<Order>? apply, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (order.History.Contains(note))
        {
            return; // A module published the same event again (say, after a redelivery). Record it once.
        }

        apply?.Invoke(order);
        order.Note(note);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A business "no". Retrying will not help, so the order fails and the fan is told.</summary>
    private async Task FailAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (order.IsFinished)
        {
            return;
        }

        order.Note(reason);
        order.Fail(reason);
        await db.SaveChangesAsync(ct);

        await NotifyFanAsync(order, () => notifier.SendFailureAsync(order, ct));
    }

    // Checkout saves the order before it publishes Order Placed, so a missing order is a real bug.
    // Throwing redelivers the message, and after too many tries it goes to the dead-letter queue.
    private async Task<Order> FindAsync(Guid orderId, CancellationToken ct) =>
        await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new InvalidOperationException($"Order {orderId} does not exist.");

    // The email is a side effect, not a saga step: if it fails, the order outcome does not change.
    private async Task NotifyFanAsync(Order order, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not email the fan about order {OrderId}", order.Id);
        }
    }
}
