using Mediator;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Notifications;
using TicketTide.Inventory.Contracts;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// Phone Tag Saga: synchronous, atomic, choreographed.
/// There is no orchestrator. Checkout is just the first link in the chain: it saves the order, calls Inventory,
/// and waits. Inventory calls Payments, Payments calls Ticketing, and the answer travels back up the same chain.
/// If a step fails, each module undoes its own step on the way back, so by the time Checkout hears about it,
/// everything has already been undone. Checkout records what happened and tells the fan.
/// </summary>
internal sealed class CheckoutStep(
    CheckoutDbContext db,
    ISender sender,
    IFanNotifier notifier,
    ILogger<CheckoutStep> logger)
{
    public async Task<Order> PlaceOrderAsync(Order order)
    {
        // A saga must not stop half-way because the fan closed the browser, so the steps
        // below do not use the request's cancellation token.
        var ct = CancellationToken.None;

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // Checkout only knows the next link, Inventory. The command carries what the rest of the chain needs.
        var result = await HoldSeatsAsync(order, ct);

        order.EventName = result.EventName;
        order.Amount = result.Amount;
        foreach (var step in result.Steps)
        {
            order.Note(step);
        }

        if (result.Succeeded)
        {
            order.TicketCodes = [.. result.TicketCodes];
            order.MoveTo(OrderStatus.Confirmed, "Tickets confirmed.");
            await db.SaveChangesAsync(ct);

            await NotifyFanAsync(order, () => notifier.SendConfirmationAsync(order, ct));
            return order;
        }

        if (result.NeedsAttention)
        {
            // Some module could not undo its step. Do not tell the fan "you have not been charged" until a person checks.
            order.FailNeedingAttention(result.FailureReason!);
            await db.SaveChangesAsync(ct);
            return order;
        }

        order.Fail(result.FailureReason!);
        await db.SaveChangesAsync(ct);

        await NotifyFanAsync(order, () => notifier.SendFailureAsync(order, ct));
        return order;
    }

    // An exception from Inventory is treated like a failed step. The details are logged, not shown to the fan.
    private async Task<HoldSeatsResult> HoldSeatsAsync(Order order, CancellationToken ct)
    {
        try
        {
            return await sender.Send(
                new HoldSeats(order.Id, order.EventId, order.Quantity, order.PaymentToken, order.SimulateTicketingFailures),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory failed for order {OrderId}", order.Id);
            return HoldSeatsResult.Rejected("Inventory is unavailable.");
        }
    }

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
