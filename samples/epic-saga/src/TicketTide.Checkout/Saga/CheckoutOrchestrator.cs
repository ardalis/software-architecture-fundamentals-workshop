using Mediator;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Notifications;
using TicketTide.Inventory.Contracts;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// Epic Saga: synchronous, atomic, orchestrated.
/// The orchestrator calls Inventory, Payments, and Ticketing one at a time and waits for each answer.
/// If any step fails, it runs the compensating commands for the steps that already succeeded, newest first,
/// and only then answers the fan. Either everything happens, or nothing does.
/// </summary>
internal sealed class CheckoutOrchestrator(
    CheckoutDbContext db,
    ISender sender,
    IFanNotifier notifier,
    ILogger<CheckoutOrchestrator> logger)
{
    private const int CompensationAttempts = 3;

    public async Task<Order> PlaceOrderAsync(Order order)
    {
        // A saga must not stop half-way because the fan closed the browser, so the steps
        // below do not use the request's cancellation token.
        var ct = CancellationToken.None;

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // The compensating command for every step that has succeeded so far.
        var compensations = new Stack<Compensation>();

        // Step 1: hold the seats.
        var hold = await RunStepAsync(() => sender.Send(new HoldSeats(order.Id, order.EventId, order.Quantity), ct),
            () => HoldSeatsResult.Rejected("Inventory is unavailable."));
        if (!hold.Succeeded)
        {
            return await FailAsync(order, $"Inventory could not hold the seats: {hold.FailureReason}", compensations);
        }

        order.EventName = hold.EventName;
        order.Amount = hold.Amount;
        order.MoveTo(OrderStatus.SeatsHeld, $"Inventory held {order.Quantity} seat(s) for {hold.EventName}.");
        compensations.Push(new Compensation("Inventory released the seats.", new ReleaseSeats(order.Id)));
        await db.SaveChangesAsync(ct);

        // Step 2: charge the card.
        var charge = await RunStepAsync(() => sender.Send(new ChargeCard(order.Id, hold.Amount, order.PaymentToken), ct),
            () => ChargeCardResult.Declined("Payments is unavailable."));
        if (!charge.Succeeded)
        {
            return await FailAsync(order, $"Payments could not charge the card: {charge.FailureReason}", compensations);
        }

        order.MoveTo(OrderStatus.PaymentCharged, $"Payments charged {Money.Format(hold.Amount)}.");
        compensations.Push(new Compensation("Payments refunded the card.", new RefundCard(order.Id)));
        await db.SaveChangesAsync(ct);

        // Step 3: issue the tickets.
        var tickets = await RunStepAsync(
            () => sender.Send(new IssueTickets(order.Id, order.EventId, order.Quantity, order.SimulateTicketingFailures), ct),
            () => IssueTicketsResult.Failed("Ticketing is unavailable."));
        if (!tickets.Succeeded)
        {
            return await FailAsync(order, $"Ticketing could not issue the tickets: {tickets.FailureReason}", compensations);
        }

        order.TicketCodes = [.. tickets.TicketCodes];
        order.MoveTo(OrderStatus.Confirmed, $"Ticketing issued {tickets.TicketCodes.Count} ticket(s). Tickets confirmed.");
        await db.SaveChangesAsync(ct);

        await NotifyFanAsync(order, () => notifier.SendConfirmationAsync(order, ct));
        return order;
    }

    private async Task<Order> FailAsync(Order order, string reason, Stack<Compensation> compensations)
    {
        var ct = CancellationToken.None;
        order.Note(reason);

        if (compensations.Count > 0)
        {
            order.MoveTo(OrderStatus.Compensating, "Compensating: undoing the steps that succeeded, newest first.");
            await db.SaveChangesAsync(ct);
        }

        while (compensations.TryPop(out var compensation))
        {
            if (await CompensateAsync(order, compensation))
            {
                order.Note(compensation.Description);
                await db.SaveChangesAsync(ct);
                continue;
            }

            // Leave the order in Compensating so an operator can see it needs attention.
            order.Note($"Compensation failed after {CompensationAttempts} attempts: {compensation.Description} Needs manual attention.");
            await db.SaveChangesAsync(ct);
            return order;
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

    private async Task<bool> CompensateAsync(Order order, Compensation compensation)
    {
        for (var attempt = 1; attempt <= CompensationAttempts; attempt++)
        {
            try
            {
                await sender.Send(compensation.Command, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Compensation {Command} failed for order {OrderId} (attempt {Attempt})",
                    compensation.Command.GetType().Name, order.Id, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
            }
        }

        return false;
    }

    // An exception from a module is treated like a failed step, so it is compensated too.
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

    private sealed record Compensation(string Description, ICommand Command);
}
