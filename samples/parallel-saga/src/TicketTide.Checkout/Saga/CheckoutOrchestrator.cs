using Mediator;
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
/// Parallel Saga: asynchronous, eventually consistent, orchestrated.
/// The orchestrator answers the fan right away, then sends Hold Seats, Charge Card, and Issue Tickets
/// to all three modules at once and does not wait. Replies arrive in any order. When all three have
/// succeeded, the fan gets the tickets by email.
/// When Ticketing fails, nothing is rolled back: the orchestrator owns the error and retries.
/// Only a business "no" (sold out, card declined) ends the order, and then the other steps are undone.
/// </summary>
internal sealed class CheckoutOrchestrator(
    CheckoutDbContext db,
    ISender sender,
    IMessageBus bus,
    IFanNotifier notifier,
    ILogger<CheckoutOrchestrator> logger)
{
    public static readonly TimeSpan TicketingRetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>If no reply to a retry arrives in this time (say the message was lost), retry again.</summary>
    public static readonly TimeSpan TicketingReplyTimeout = TimeSpan.FromSeconds(30);

    public async Task<Order> StartAsync(Order order, CancellationToken ct)
    {
        // Price the order first. Payments needs the amount at the same time Inventory gets the hold request.
        var price = await sender.Send(new GetEventPrice(order.EventId), ct);
        if (price is null)
        {
            order.Fail($"Event '{order.EventId}' does not exist.");
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            return order;
        }

        order.EventName = price.EventName;
        order.Amount = price.Price * order.Quantity;
        order.Note("Sent Hold Seats, Charge Card, and Issue Tickets in parallel.");
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // Send all three commands at once. Each module works at its own pace and replies when it is done.
        // Nothing is saved after this point: a fast reply may already be updating the order.
        await bus.PublishAsync(new HoldSeats(order.Id, order.EventId, order.Quantity), ct);
        await bus.PublishAsync(new ChargeCard(order.Id, order.Amount.Value, order.PaymentToken), ct);
        await bus.PublishAsync(new IssueTickets(order.Id, order.EventId, order.Quantity, order.SimulateTicketingFailures), ct);
        return order;
    }

    public Task SeatsHeldAsync(Guid orderId, CancellationToken ct) =>
        StepSucceededAsync(orderId, order =>
        {
            order.SeatsHeld = true;
            order.Note($"Inventory held {order.Quantity} seat(s) for {order.EventName}.");
        }, ct);

    public Task CardChargedAsync(Guid orderId, CancellationToken ct) =>
        StepSucceededAsync(orderId, order =>
        {
            order.PaymentCharged = true;
            order.Note($"Payments charged {Money.Format(order.Amount ?? 0m)}.");
        }, ct);

    public Task TicketsIssuedAsync(Guid orderId, IReadOnlyList<string> ticketCodes, CancellationToken ct) =>
        StepSucceededAsync(orderId, order =>
        {
            order.TicketsIssued = true;
            order.RetryTicketingAt = null;
            order.TicketCodes = [.. ticketCodes];
            order.Note($"Ticketing issued {ticketCodes.Count} ticket(s).");
        }, ct);

    /// <summary>A technical failure. Nothing is undone; the orchestrator schedules a retry.</summary>
    public async Task TicketingFailedAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (order.IsFinished || order.TicketsIssued)
        {
            return; // A late reply for an order that is already done.
        }

        order.RetryTicketingAt = DateTimeOffset.UtcNow + TicketingRetryDelay;
        order.Note($"Ticketing failed: {reason} Will retry.");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Called by <see cref="TicketingRetryWorker"/> when a retry is due.</summary>
    public async Task RetryTicketingAsync(Order order, CancellationToken ct)
    {
        order.RetryTicketingAt = DateTimeOffset.UtcNow + TicketingReplyTimeout;
        order.TicketingRetries++;
        order.Note($"Retry {order.TicketingRetries}: sent Issue Tickets again.");
        await db.SaveChangesAsync(ct); // Saved first, so a concurrent reply can't schedule a second retry.

        await bus.PublishAsync(new IssueTickets(order.Id, order.EventId, order.Quantity, order.SimulateTicketingFailures), ct);
    }

    /// <summary>A business "no": the order cannot succeed, so undo whatever the other modules did.</summary>
    public async Task StepRejectedAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (order.Status == OrderStatus.Failed)
        {
            // Already failed. This may be a redelivery because publishing the compensations failed last time,
            // so send them again. Every compensation is idempotent, so sending one twice is safe.
            await CompensateAllStepsAsync(order.Id, ct);
            return;
        }

        if (order.IsFinished)
        {
            return;
        }

        order.Note(reason);
        order.Fail(reason);
        await db.SaveChangesAsync(ct);

        await CompensateAllStepsAsync(order.Id, ct);
        await NotifyFanAsync(order, () => notifier.SendFailureAsync(order, ct));
    }

    // The other replies may not have arrived yet, so compensate all three steps.
    // Each module ignores a compensation for work it never did, and remembers it in case
    // the original command arrives later.
    private async Task CompensateAllStepsAsync(Guid orderId, CancellationToken ct)
    {
        await bus.PublishAsync(new ReleaseSeats(orderId), ct);
        await bus.PublishAsync(new RefundCard(orderId), ct);
        await bus.PublishAsync(new VoidTickets(orderId), ct);
    }

    public async Task CompensatedAsync(Guid orderId, string note, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (!order.History.Contains(note))
        {
            order.Note(note);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task StepSucceededAsync(Guid orderId, Action<Order> apply, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (order.IsFinished)
        {
            return; // A late reply. If the order failed, the compensation already covers this step.
        }

        apply(order);
        var confirmed = order is { SeatsHeld: true, PaymentCharged: true, TicketsIssued: true };
        if (confirmed)
        {
            order.Confirm();
        }

        await db.SaveChangesAsync(ct);

        if (confirmed)
        {
            await NotifyFanAsync(order, () => notifier.SendConfirmationAsync(order, ct));
        }
    }

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
