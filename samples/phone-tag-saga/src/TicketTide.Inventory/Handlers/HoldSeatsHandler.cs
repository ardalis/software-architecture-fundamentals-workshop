using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Payments.Contracts;

namespace TicketTide.Inventory.Handlers;

/// <summary>
/// Inventory's link in the Phone Tag chain: hold the seats, call Payments and wait for its answer,
/// and release the seats if anything further down the chain failed.
/// Inventory knows only the next step (Payments) and its own undo step.
/// </summary>
public sealed class HoldSeatsHandler(InventoryDbContext db, ISender sender, ILogger<HoldSeatsHandler> logger)
    : ICommandHandler<HoldSeats, HoldSeatsResult>
{
    private const int CompensationAttempts = 3;

    public async ValueTask<HoldSeatsResult> Handle(HoldSeats command, CancellationToken cancellationToken)
    {
        // This module's own step: hold the seats. Its local transaction commits before the next call,
        // so no database lock is held while the rest of the chain runs.
        var hold = await HoldAsync(command, cancellationToken);
        if (!hold.Succeeded)
        {
            return HoldSeatsResult.Rejected(hold.FailureReason!);
        }

        var steps = new List<string> { $"Inventory held {command.Quantity} seat(s) for {hold.EventName}." };

        // The next step: Inventory calls Payments directly and waits. Payments in turn calls Ticketing.
        var charge = await ChargeCardAsync(command, hold.Amount, cancellationToken);
        steps.AddRange(charge.Steps);
        if (charge.Succeeded)
        {
            return HoldSeatsResult.Completed(hold.EventName, hold.Amount, charge.TicketCodes, steps);
        }

        // Something further down the chain failed, and the saga is atomic: undo this module's step
        // before answering Checkout. Inventory always releases its own seats, whatever happened downstream.
        if (await ReleaseWithRetriesAsync(command.OrderId))
        {
            steps.Add("Inventory released the seats.");
            return HoldSeatsResult.Undone(hold.EventName, hold.Amount, charge.FailureReason!, steps, charge.NeedsAttention);
        }

        steps.Add($"Inventory could not release the seats after {CompensationAttempts} attempts. Needs manual attention.");
        return HoldSeatsResult.Undone(hold.EventName, hold.Amount, charge.FailureReason!, steps, needsAttention: true);
    }

    private async Task<SeatHoldOutcome> HoldAsync(HoldSeats command, CancellationToken cancellationToken)
    {
        var existing = await db.SeatHolds.AsNoTracking()
            .FirstOrDefaultAsync(h => h.OrderId == command.OrderId, cancellationToken);
        if (existing is not null)
        {
            var name = await db.Events.Where(e => e.Id == existing.EventId).Select(e => e.Name).FirstAsync(cancellationToken);
            return existing.Status == SeatHoldStatus.Held
                ? SeatHoldOutcome.Held(name, existing.Amount)
                : SeatHoldOutcome.Rejected("The seats for this order were already released.");
        }

        var @event = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken);
        if (@event is null)
        {
            return SeatHoldOutcome.Rejected($"Event '{command.EventId}' does not exist.");
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
            return SeatHoldOutcome.Rejected($"Not enough seats left for {@event.Name}.");
        }

        logger.LogInformation("Held {Quantity} seats for order {OrderId}", command.Quantity, command.OrderId);
        return SeatHoldOutcome.Held(@event.Name, @event.Price * command.Quantity);
    }

    // An exception from Payments is treated like a failed step, so the seats are released too.
    // The details are logged, not shown to the fan.
    private async Task<ChargeCardResult> ChargeCardAsync(HoldSeats command, decimal amount, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.Send(
                new ChargeCard(command.OrderId, amount, command.PaymentToken, command.EventId, command.Quantity,
                    command.SimulateTicketingFailures),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payments failed for order {OrderId}", command.OrderId);
            return ChargeCardResult.Declined("Payments is unavailable.");
        }
    }

    private async Task<bool> ReleaseWithRetriesAsync(Guid orderId)
    {
        for (var attempt = 1; attempt <= CompensationAttempts; attempt++)
        {
            try
            {
                await ReleaseAsync(orderId, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Releasing seats failed for order {OrderId} (attempt {Attempt})", orderId, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
            }
        }

        return false;
    }

    // The compensating action. It lives here, inside Inventory, because no other module knows how to undo a hold.
    private async Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var hold = await db.SeatHolds.FirstOrDefaultAsync(h => h.OrderId == orderId, ct);
            if (hold is null || hold.Status == SeatHoldStatus.Released)
            {
                return; // Nothing to undo. Compensations must be idempotent.
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

    private sealed record SeatHoldOutcome(bool Succeeded, string EventName, decimal Amount, string? FailureReason)
    {
        public static SeatHoldOutcome Held(string eventName, decimal amount) => new(true, eventName, amount, null);

        public static SeatHoldOutcome Rejected(string reason) => new(false, string.Empty, 0m, reason);
    }
}
