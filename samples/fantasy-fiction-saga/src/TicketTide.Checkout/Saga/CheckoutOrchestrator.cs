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
/// Fantasy Fiction Saga: asynchronous, atomic, orchestrated.
/// The orchestrator sends Hold Seats, Charge Card, and Issue Tickets to all three modules at once and does not wait.
/// Replies arrive in any order. If all three succeed, the order is confirmed.
/// The saga is atomic, so there are no retries: any failure undoes the whole purchase. The orchestrator compensates
/// every step that succeeded, keeps tracking the requests still in flight, and undoes late successes too.
/// Only when every reply is in, and every compensation is confirmed, does the waiting fan get an answer.
/// </summary>
internal sealed class CheckoutOrchestrator(
    CheckoutDbContext db,
    ISender sender,
    IMessageBus bus,
    OrderCompletionSignal completion,
    IFanNotifier notifier,
    ILogger<CheckoutOrchestrator> logger)
{
    public async Task<Order> StartAsync(Order order, CancellationToken ct)
    {
        // Price the order first. Payments needs the amount at the same time Inventory gets the hold request.
        var price = await sender.Send(new GetEventPrice(order.EventId), ct);
        if (price is null)
        {
            order.Reject($"Event '{order.EventId}' does not exist.");
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            return order;
        }

        order.EventName = price.EventName;
        order.Amount = price.Price * order.Quantity;

        // Mark all three steps as in flight and save, before anything is sent, so every reply finds them.
        foreach (var step in Order.Steps)
        {
            order.MoveStep(step, StepState.Requested);
        }

        order.Note("Sent Hold Seats, Charge Card, and Issue Tickets in parallel. Waiting for all three replies.");
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // The fan's request waits for this order. Register before sending, so even the fastest reply is not missed.
        completion.Register(order.Id);

        // Send all three commands at once. Each module works at its own pace and replies when it is done.
        await bus.PublishAsync(new HoldSeats(order.Id, order.EventId, order.Quantity), ct);
        await bus.PublishAsync(new ChargeCard(order.Id, order.Amount.Value, order.PaymentToken), ct);
        await bus.PublishAsync(new IssueTickets(order.Id, order.EventId, order.Quantity, order.SimulateTicketingFailures), ct);
        return order;
    }

    public Task SeatsHeldAsync(Guid orderId, CancellationToken ct) =>
        StepSucceededAsync(orderId, SagaStep.HoldSeats,
            order => $"Inventory held {order.Quantity} seat(s) for {order.EventName}.", ct);

    public Task CardChargedAsync(Guid orderId, CancellationToken ct) =>
        StepSucceededAsync(orderId, SagaStep.ChargeCard,
            order => $"Payments charged {Money.Format(order.Amount ?? 0m)}.", ct);

    public Task TicketsIssuedAsync(Guid orderId, IReadOnlyList<string> ticketCodes, CancellationToken ct) =>
        StepSucceededAsync(orderId, SagaStep.IssueTickets, order =>
        {
            order.TicketCodes = [.. ticketCodes];
            return $"Ticketing issued {ticketCodes.Count} ticket(s).";
        }, ct);

    /// <summary>
    /// A step failed: sold out, card declined, or Ticketing is down. The saga is atomic, so there is no retry.
    /// The whole purchase must be undone.
    /// </summary>
    public async Task StepFailedAsync(Guid orderId, SagaStep step, string reason, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (order.StateOf(step) != StepState.Requested)
        {
            // A duplicate reply. This step's answer is already recorded.
            await ResendCompensationsAsync(order, ct);
            return;
        }

        order.MoveStep(step, StepState.Failed);
        order.Note(reason);

        // The first failure starts compensating every step that has succeeded so far.
        // Steps still in flight are compensated when (and if) their success reply arrives.
        IReadOnlyList<SagaStep> toUndo = [];
        if (order.Status == OrderStatus.Pending)
        {
            toUndo = order.StepsThatAre(StepState.Succeeded);
            order.StartCompensating(reason);
            foreach (var succeeded in toUndo)
            {
                order.MoveStep(succeeded, StepState.Compensating);
                order.Note($"Sent {CompensationName(succeeded)}.");
            }
        }

        // A second failure while compensating has nothing to undo, but it may be the last reply we were waiting for.
        var failed = order.TryFinishCompensating();

        // Saved before the compensations are sent, so their confirmations find the step marked Compensating.
        await db.SaveChangesAsync(ct);

        foreach (var succeeded in toUndo)
        {
            await SendCompensationAsync(order.Id, succeeded, ct);
        }

        if (failed)
        {
            await FinishAsync(order, () => notifier.SendFailureAsync(order, ct));
        }
    }

    /// <summary>A module confirmed that it undid its step.</summary>
    public async Task CompensatedAsync(Guid orderId, SagaStep step, string note, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (order.StateOf(step) != StepState.Compensating)
        {
            return; // A duplicate confirmation.
        }

        order.MoveStep(step, StepState.Compensated);
        order.Note(note);
        var failed = order.TryFinishCompensating();
        await db.SaveChangesAsync(ct);

        if (failed)
        {
            await FinishAsync(order, () => notifier.SendFailureAsync(order, ct));
        }
    }

    private async Task StepSucceededAsync(Guid orderId, SagaStep step, Func<Order, string> apply, CancellationToken ct)
    {
        var order = await FindAsync(orderId, ct);
        if (order.StateOf(step) != StepState.Requested)
        {
            // A duplicate reply. This step's answer is already recorded.
            await ResendCompensationsAsync(order, ct);
            return;
        }

        if (order.Status == OrderStatus.Compensating)
        {
            // A late reply: another step already failed. This step succeeded anyway, so it must be undone too.
            order.MoveStep(step, StepState.Compensating);
            order.Note($"Late reply: {apply(order)} The purchase is being undone, so sent {CompensationName(step)}.");
            await db.SaveChangesAsync(ct);

            await SendCompensationAsync(order.Id, step, ct);
            return;
        }

        order.MoveStep(step, StepState.Succeeded);
        order.Note(apply(order));
        var confirmed = order.StepsThatAre(StepState.Succeeded).Count == Order.Steps.Length;
        if (confirmed)
        {
            order.Confirm();
        }

        await db.SaveChangesAsync(ct);

        if (confirmed)
        {
            await FinishAsync(order, () => notifier.SendConfirmationAsync(order, ct));
        }
    }

    /// <summary>
    /// The state is saved before the compensations are sent. If sending one failed, the reply is retried, finds
    /// its step already recorded, and lands here. The saved state still says which compensations are in flight,
    /// so send them all again. That is safe: a module that already undid its step just confirms again.
    /// </summary>
    private async Task ResendCompensationsAsync(Order order, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Compensating)
        {
            return;
        }

        foreach (var step in order.StepsThatAre(StepState.Compensating))
        {
            await SendCompensationAsync(order.Id, step, ct);
        }
    }

    private static string CompensationName(SagaStep step) => step switch
    {
        SagaStep.HoldSeats => "Release Seats",
        SagaStep.ChargeCard => "Refund Card",
        SagaStep.IssueTickets => "Void Tickets",
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    private Task SendCompensationAsync(Guid orderId, SagaStep step, CancellationToken ct) => step switch
    {
        SagaStep.HoldSeats => bus.PublishAsync(new ReleaseSeats(orderId), ct),
        SagaStep.ChargeCard => bus.PublishAsync(new RefundCard(orderId), ct),
        SagaStep.IssueTickets => bus.PublishAsync(new VoidTickets(orderId), ct),
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    private async Task<Order> FindAsync(Guid orderId, CancellationToken ct) =>
        await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new InvalidOperationException($"Order {orderId} does not exist.");

    /// <summary>The order reached its final state: release the waiting fan, then email them.</summary>
    private async Task FinishAsync(Order order, Func<Task> sendEmail)
    {
        completion.Complete(order.Id);

        // The email is a side effect, not a saga step: if it fails, the order outcome does not change.
        try
        {
            await sendEmail();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not email the fan about order {OrderId}", order.Id);
        }
    }
}
