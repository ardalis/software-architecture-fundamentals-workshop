using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Payments.Contracts;

namespace TicketTide.Inventory.Handlers;

/// <summary>
/// Inventory's link in the chain: hold the seats and commit, then hand off to Payments and wait for its reply.
/// There is no orchestrator to clean up after a failure, so Inventory handles its own: if the card is not
/// charged, Inventory gives its seats back before it replies.
/// </summary>
public sealed class HoldSeatsHandler(InventoryDbContext db, ISender sender, ILogger<HoldSeatsHandler> logger)
    : ICommandHandler<HoldSeats, HoldSeatsResult>
{
    public async ValueTask<HoldSeatsResult> Handle(HoldSeats command, CancellationToken cancellationToken)
    {
        // Step 1: hold the seats in Inventory's own database, and commit.
        var hold = await HoldAsync(command, cancellationToken);
        if (hold.FailureReason is not null)
        {
            var reason = $"Inventory could not hold the seats: {hold.FailureReason}";
            return new HoldSeatsResult(HoldSeatsOutcome.Failed, null, null, [], reason, [reason]);
        }

        List<string> steps = [$"Inventory held {command.Quantity} seat(s) for {hold.EventName}."];

        // Step 2: hand off to the next link in the chain, and wait for the replies to travel back up.
        var charge = await ChargeCardAsync(command, hold.Amount, cancellationToken);
        if (charge is null)
        {
            // Payments did not answer, so nobody knows whether the card was charged. Giving the seats back, or
            // telling the fan they were not charged, could both be wrong. So the seats stay held, and the order
            // stays Pending until a person checks it.
            steps.Add("Payments did not answer, so Inventory cannot tell whether the card was charged. The seats stay held. Needs manual attention.");
            return new HoldSeatsResult(HoldSeatsOutcome.Pending, hold.EventName, hold.Amount, [], null, steps);
        }

        steps.AddRange(charge.Steps);

        switch (charge.Outcome)
        {
            case ChargeCardOutcome.Done:
                return new HoldSeatsResult(HoldSeatsOutcome.Done, hold.EventName, hold.Amount, charge.TicketCodes, null, steps);

            case ChargeCardOutcome.Pending:
                // The card is charged and Payments owns the Ticketing hand-off, so the seats stay held.
                return new HoldSeatsResult(HoldSeatsOutcome.Pending, hold.EventName, hold.Amount, [], null, steps);

            default:
                // A business rejection, such as a declined card, is not retried. Inventory undoes its own step.
                steps.Add(await TryReleaseAsync(command.OrderId, cancellationToken)
                    ? "Inventory released the seats."
                    : "Inventory could not release the seats. Needs manual attention.");
                return new HoldSeatsResult(HoldSeatsOutcome.Failed, hold.EventName, hold.Amount, [], charge.FailureReason, steps);
        }
    }

    // Returns null when Payments threw instead of answering. That is not a decline: Payments may have charged the
    // card before it failed. The details are logged, not shown to the fan.
    private async Task<ChargeCardResult?> ChargeCardAsync(HoldSeats command, decimal amount, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.Send(
                new ChargeCard(command.OrderId, command.EventId, command.Quantity, amount, command.PaymentToken, command.SimulateTicketingFailures),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payments failed for order {OrderId}. Needs manual attention.", command.OrderId);
            return null;
        }
    }

    private async Task<Hold> HoldAsync(HoldSeats command, CancellationToken cancellationToken)
    {
        var existing = await db.SeatHolds.AsNoTracking()
            .FirstOrDefaultAsync(h => h.OrderId == command.OrderId, cancellationToken);
        if (existing is not null)
        {
            var name = await db.Events.Where(e => e.Id == existing.EventId).Select(e => e.Name).FirstAsync(cancellationToken);
            return existing.Status == SeatHoldStatus.Held
                ? new Hold(name, existing.Amount, null)
                : new Hold(name, existing.Amount, "The seats for this order were already released.");
        }

        var @event = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken);
        if (@event is null)
        {
            return new Hold(string.Empty, 0m, $"Event '{command.EventId}' does not exist.");
        }

        // The seat count and the hold are written in one local transaction.
        // The conditional UPDATE stops two fans from taking the last seats at the same time.
        var strategy = db.Database.CreateExecutionStrategy();
        var held = await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var updated = await db.Events
                .Where(e => e.Id == command.EventId && e.Capacity - e.SeatsHeld >= command.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsHeld, e => e.SeatsHeld + command.Quantity), ct);
            if (updated == 0)
            {
                return false;
            }

            db.SeatHolds.Add(new SeatHold
            {
                OrderId = command.OrderId,
                EventId = command.EventId,
                Quantity = command.Quantity,
                Amount = @event.Price * command.Quantity,
                Status = SeatHoldStatus.Held,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }, cancellationToken);

        if (!held)
        {
            logger.LogInformation("Not enough seats left for order {OrderId}", command.OrderId);
            return new Hold(@event.Name, 0m, $"Not enough seats left for {@event.Name}.");
        }

        logger.LogInformation("Held {Quantity} seats for order {OrderId}", command.Quantity, command.OrderId);
        return new Hold(@event.Name, @event.Price * command.Quantity, null);
    }

    // Returns false when the seats could not be given back. The fan still was not charged, so the reply stays
    // Failed, but the seats are stuck as held, so the error is logged and the reply says it needs attention.
    private async Task<bool> TryReleaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            await ReleaseAsync(orderId, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not release the seats for order {OrderId}. Needs manual attention.", orderId);
            return false;
        }
    }

    private async Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var hold = await db.SeatHolds.FirstOrDefaultAsync(h => h.OrderId == orderId, ct);
            if (hold is null || hold.Status == SeatHoldStatus.Released)
            {
                return; // Nothing to give back.
            }

            hold.Status = SeatHoldStatus.Released;
            hold.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await db.Events
                .Where(e => e.Id == hold.EventId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsHeld, e => e.SeatsHeld - hold.Quantity), ct);

            await transaction.CommitAsync(ct);
            logger.LogInformation("Released {Quantity} seats for order {OrderId}", hold.Quantity, orderId);
        }, cancellationToken);
    }

    private sealed record Hold(string EventName, decimal Amount, string? FailureReason);
}
