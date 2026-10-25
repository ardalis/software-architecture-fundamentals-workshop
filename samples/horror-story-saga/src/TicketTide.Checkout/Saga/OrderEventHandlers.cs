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
/// Horror Story Saga: asynchronous, atomic, choreographed.
/// Checkout started the saga by publishing Order Placed. From then on it coordinates nothing: Inventory, Payments,
/// and Ticketing each react to the event before them. Checkout listens to every event to keep the order's history,
/// but only three of them finish the order:
/// <list type="bullet">
/// <item><see cref="TicketsIssued"/>: every step succeeded. Confirm the order.</item>
/// <item><see cref="SeatsRejected"/>: the first step failed, so there is nothing to undo. Fail the order.</item>
/// <item><see cref="SeatsReleased"/>: the last compensation. Only now is everything undone, so fail the order.</item>
/// </list>
/// Each handler is idempotent: a redelivered event finds the note already in the history and changes nothing.
/// </summary>
internal sealed class OrderEventHandlers(
    CheckoutDbContext db,
    OrderCompletionSignal completion,
    IFanNotifier notifier,
    ILogger<OrderEventHandlers> logger) :
    IMessageHandler<SeatsHeld>,
    IMessageHandler<SeatsRejected>,
    IMessageHandler<SeatsReleased>,
    IMessageHandler<PaymentReceived>,
    IMessageHandler<PaymentDeclined>,
    IMessageHandler<CardRefunded>,
    IMessageHandler<TicketsIssued>,
    IMessageHandler<TicketIssueFailed>
{
    public Task HandleAsync(SeatsHeld message, MessageContext context, CancellationToken ct) =>
        UpdateAsync(message.OrderId, order =>
        {
            order.EventName = message.EventName;
            order.Amount = message.Amount;
            order.Note($"Inventory held {message.Quantity} seat(s) for {message.EventName}.");
        }, ct);

    public Task HandleAsync(PaymentReceived message, MessageContext context, CancellationToken ct) =>
        UpdateAsync(message.OrderId, order => order.Note($"Payments charged {Money.Format(message.Amount)}."), ct);

    public async Task HandleAsync(TicketsIssued message, MessageContext context, CancellationToken ct)
    {
        var order = await FindAsync(message.OrderId, ct);
        if (order.Status != OrderStatus.Pending)
        {
            return; // Already confirmed by an earlier delivery of this event.
        }

        order.EventName = message.EventName;
        order.Amount = message.Amount;
        order.TicketCodes = [.. message.TicketCodes];
        order.Note($"Ticketing issued {message.TicketCodes.Count} ticket(s).");
        order.Confirm();
        await db.SaveChangesAsync(ct);

        await FinishAsync(order, () => notifier.SendConfirmationAsync(order, ct));
    }

    // A failure, or a compensation that is not the last one. The order cannot succeed now,
    // but Checkout must not tell the fan anything until every compensation is in.

    public Task HandleAsync(PaymentDeclined message, MessageContext context, CancellationToken ct) =>
        UpdateAsync(message.OrderId, order =>
            order.StartCompensating($"Payments could not charge the card: {message.Reason}"), ct);

    public Task HandleAsync(TicketIssueFailed message, MessageContext context, CancellationToken ct) =>
        UpdateAsync(message.OrderId, order =>
            order.StartCompensating($"Ticketing could not issue the tickets: {message.Reason}"), ct);

    public Task HandleAsync(CardRefunded message, MessageContext context, CancellationToken ct) =>
        UpdateAsync(message.OrderId, order => order.StartCompensating("Payments refunded the card."), ct);

    public async Task HandleAsync(SeatsRejected message, MessageContext context, CancellationToken ct)
    {
        var order = await FindAsync(message.OrderId, ct);
        if (order.IsFinished)
        {
            return;
        }

        var reason = $"Inventory could not hold the seats: {message.Reason}";
        order.Note(reason);
        order.Fail(reason, "Nothing to undo. Purchase failed.");
        await db.SaveChangesAsync(ct);

        await FinishAsync(order, () => notifier.SendFailureAsync(order, ct));
    }

    /// <summary>
    /// The last compensation. Inventory only publishes it once the services after it have undone their steps,
    /// so now the whole purchase is undone and the fan can be told.
    /// </summary>
    public async Task HandleAsync(SeatsReleased message, MessageContext context, CancellationToken ct)
    {
        var order = await FindAsync(message.OrderId, ct);
        if (order.IsFinished)
        {
            return;
        }

        order.Note($"Inventory released the seats. It heard {string.Join(", then ", message.Heard)}.");
        order.Fail(message.Reason, "Every compensation is in. Purchase failed.");
        await db.SaveChangesAsync(ct);

        await FinishAsync(order, () => notifier.SendFailureAsync(order, ct));
    }

    /// <summary>Records an event that does not finish the order. Events can arrive late, so this works in any state.</summary>
    private async Task UpdateAsync(Guid orderId, Action<Order> apply, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        apply(order);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Order> FindAsync(Guid orderId, CancellationToken ct) =>
        await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new InvalidOperationException($"Order {orderId} does not exist.");

    // The order is saved in its final state. Wake the waiting fan first, then send the email.
    // The email is a side effect, not a saga step: if it fails, the order outcome does not change.
    private async Task FinishAsync(Order order, Func<Task> email)
    {
        completion.Complete(order.Id);
        try
        {
            await email();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not email the fan about order {OrderId}", order.Id);
        }
    }
}
