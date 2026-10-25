using Mediator;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Notifications;
using TicketTide.Inventory.Contracts;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// Time Travel Saga: synchronous, eventually consistent, choreographed.
/// Checkout is not an orchestrator. It saves the order and calls the first link in the chain, Inventory.
/// Inventory holds the seats and calls Payments; Payments charges the card and calls Ticketing. Each module
/// commits its own work before it hands off, and handles its own errors. Checkout only sees the reply that
/// travels back up the chain: Done, Pending (Payments is still retrying Ticketing), or Failed.
/// </summary>
internal sealed class CheckoutService(
    CheckoutDbContext db,
    ISender sender,
    IFanNotifier notifier,
    ILogger<CheckoutService> logger)
{
    public async Task<Order> PlaceOrderAsync(Order order)
    {
        // A saga must not stop half-way because the fan closed the browser, so the chain below
        // does not use the request's cancellation token.
        var ct = CancellationToken.None;

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // One call. It returns when the whole chain has replied.
        var reply = await StartChainAsync(order, ct);
        if (reply is null)
        {
            // An error is not a "no": the seats may be held and the card charged. Undo nothing, tell the fan nothing
            // false, and leave the order for support.
            order.MoveTo(OrderStatus.NeedsAttention, "Inventory failed unexpectedly, so the outcome is unknown. Needs manual attention.");
            await db.SaveChangesAsync(ct);
            return order;
        }

        order.EventName = reply.EventName;
        order.Amount = reply.Amount;
        foreach (var step in reply.Steps)
        {
            order.Note(step);
        }

        switch (reply.Outcome)
        {
            case HoldSeatsOutcome.Done:
                order.TicketCodes = [.. reply.TicketCodes];
                order.MoveTo(OrderStatus.Confirmed, "Tickets confirmed.");
                break;

            case HoldSeatsOutcome.Pending:
                // Nothing failed for good, so nothing is undone. The data catches up later:
                // Payments tells Checkout (TicketsIssued) when Ticketing finally issues the tickets.
                order.MoveTo(OrderStatus.Pending, "Tickets pending. Checkout will confirm the order when Payments reports the tickets issued.");
                break;

            default:
                order.Fail(reply.FailureReason ?? "The purchase failed.");
                break;
        }

        await db.SaveChangesAsync(ct);

        await notifier.TryNotifyAsync(order, logger, ct);
        return order;
    }

    // An exception from Inventory means the outcome is unknown, so it returns null. The details are logged, not shown to the fan.
    private async Task<HoldSeatsResult?> StartChainAsync(Order order, CancellationToken ct)
    {
        try
        {
            return await sender.Send(
                new HoldSeats(order.Id, order.EventId, order.Quantity, order.PaymentToken, order.SimulateTicketingFailures), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory failed for order {OrderId}", order.Id);
            return null;
        }
    }
}
