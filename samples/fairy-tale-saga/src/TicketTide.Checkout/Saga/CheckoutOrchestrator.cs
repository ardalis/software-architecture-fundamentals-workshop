using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Notifications;
using TicketTide.Inventory.Contracts;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// Fairy Tale Saga: synchronous, eventually consistent, orchestrated.
/// The orchestrator calls Inventory, Payments, and Ticketing one at a time and waits for each answer.
/// Each module commits its own work right away, and nothing is rolled back when Ticketing fails.
/// Instead the order becomes <see cref="OrderStatus.Pending"/>, the fan hears that the tickets are on the way,
/// and the orchestrator owns the error: <see cref="TicketingRetryService"/> retries Issue Tickets until it works.
/// A business "no" (sold out, card declined) is not something a retry can fix, so that order fails
/// and the orchestrator releases any seats it held.
/// </summary>
internal sealed class CheckoutOrchestrator(
    CheckoutDbContext db,
    ISender sender,
    IFanNotifier notifier,
    ILogger<CheckoutOrchestrator> logger)
{
    /// <summary>How long to wait between Issue Tickets attempts. Short, so the demo is quick.</summary>
    public static readonly TimeSpan TicketingRetryDelay = TimeSpan.FromSeconds(1);

    private const int ReleaseAttempts = 3;

    public async Task<Order> PlaceOrderAsync(Order order)
    {
        // A saga must not stop half-way because the fan closed the browser, so the steps
        // below do not use the request's cancellation token.
        var ct = CancellationToken.None;

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // Step 1: hold the seats.
        var hold = await RunStepAsync(() => sender.Send(new HoldSeats(order.Id, order.EventId, order.Quantity), ct),
            () => HoldSeatsResult.Rejected("Inventory is unavailable."));
        if (!hold.Succeeded)
        {
            return await FailAsync(order, $"Inventory could not hold the seats: {hold.FailureReason}", seatsHeld: false);
        }

        order.EventName = hold.EventName;
        order.Amount = hold.Amount;
        order.MoveTo(OrderStatus.SeatsHeld, $"Inventory held {order.Quantity} seat(s) for {hold.EventName}.");
        await db.SaveChangesAsync(ct);

        // Step 2: charge the card. A declined card is a business failure: retrying will not change the answer.
        var charge = await RunStepAsync(() => sender.Send(new ChargeCard(order.Id, hold.Amount, order.PaymentToken), ct),
            () => ChargeCardResult.Declined("Payments is unavailable."));
        if (!charge.Succeeded)
        {
            return await FailAsync(order, $"Payments could not charge the card: {charge.FailureReason}", seatsHeld: true);
        }

        order.MoveTo(OrderStatus.PaymentCharged, $"Payments charged {Money.Format(hold.Amount)}.");
        await db.SaveChangesAsync(ct);

        // Step 3: issue the tickets. If Ticketing fails, keep the seats and the charge, and retry later.
        var tickets = await IssueTicketsAsync(order);
        if (!tickets.Succeeded)
        {
            await WaitForTicketsAsync(order, $"Ticketing could not issue the tickets: {tickets.FailureReason} Will retry.");
            return order;
        }

        await ConfirmAsync(order, tickets.TicketCodes, $"Ticketing issued {tickets.TicketCodes.Count} ticket(s). Tickets confirmed.");
        return order;
    }

    /// <summary>
    /// Retries Issue Tickets for a pending order. <see cref="TicketingRetryService"/> calls this when the retry is due.
    /// Issuing tickets is idempotent, so retrying an order that Ticketing already finished is safe.
    /// </summary>
    public async Task RetryIssueTicketsAsync(Guid orderId)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.Status == OrderStatus.Pending);
        if (order is null)
        {
            return; // Already confirmed: nothing to retry.
        }

        var retry = order.StartTicketingRetry();
        var tickets = await IssueTicketsAsync(order);
        if (!tickets.Succeeded)
        {
            await WaitForTicketsAsync(order, $"Retry {retry}: Ticketing could not issue the tickets: {tickets.FailureReason} Will retry.");
            return;
        }

        await ConfirmAsync(order, tickets.TicketCodes,
            $"Retry {retry}: Ticketing issued {tickets.TicketCodes.Count} ticket(s). Tickets confirmed.");
    }

    private Task<IssueTicketsResult> IssueTicketsAsync(Order order) =>
        RunStepAsync(
            () => sender.Send(new IssueTickets(order.Id, order.EventId, order.Quantity, order.SimulateTicketingFailures), CancellationToken.None),
            () => IssueTicketsResult.Failed("Ticketing is unavailable."));

    // The order is in and paid for, so it stays that way. The next attempt time is saved in checkoutdb,
    // so the retry still happens if the API restarts.
    private async Task WaitForTicketsAsync(Order order, string note)
    {
        order.WaitForTickets(note, TicketingRetryDelay);
        await db.SaveChangesAsync(CancellationToken.None);
        logger.LogWarning("Order {OrderId} is pending: {Note}", order.Id, note);
    }

    private async Task ConfirmAsync(Order order, IReadOnlyList<string> ticketCodes, string note)
    {
        order.Confirm(ticketCodes, note);
        await db.SaveChangesAsync(CancellationToken.None);

        await NotifyFanAsync(order, () => notifier.SendConfirmationAsync(order, CancellationToken.None));
    }

    // Only failures before the card is charged end up here, so the only work to undo is the seat hold.
    // Nothing after the charge is ever rolled back in this saga.
    private async Task<Order> FailAsync(Order order, string reason, bool seatsHeld)
    {
        var ct = CancellationToken.None;
        order.Note(reason);

        if (seatsHeld)
        {
            order.Note(await ReleaseSeatsAsync(order)
                ? "Inventory released the seats."
                : $"Inventory could not release the seats after {ReleaseAttempts} attempts. Needs manual attention.");
        }

        order.Fail(reason);
        await db.SaveChangesAsync(ct);

        await NotifyFanAsync(order, () => notifier.SendFailureAsync(order, ct));
        return order;
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

    private async Task<bool> ReleaseSeatsAsync(Order order)
    {
        for (var attempt = 1; attempt <= ReleaseAttempts; attempt++)
        {
            try
            {
                await sender.Send(new ReleaseSeats(order.Id), CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Releasing the seats failed for order {OrderId} (attempt {Attempt})", order.Id, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
            }
        }

        return false;
    }

    // An exception from a module is treated like a failed step. For Ticketing, that means "retry later".
    // The details are logged, not shown to the fan.
    private async Task<T> RunStepAsync<T>(Func<ValueTask<T>> step, Func<T> onException)
    {
        try
        {
            return await step();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saga step failed");
            return onException();
        }
    }
}
