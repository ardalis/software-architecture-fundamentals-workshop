using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Handoffs;
using TicketTide.Payments.Provider;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Payments.Handlers;

/// <summary>
/// Payments' link in the chain: charge the card and commit, then hand off to Ticketing.
/// Payments owns that hand-off. When Ticketing fails, nothing is rolled back: Payments retries a few times while
/// the fan waits, and if Ticketing is still down, it records the hand-off and keeps retrying in the background.
/// </summary>
public sealed class ChargeCardHandler(
    PaymentsDbContext db,
    IPaymentProvider provider,
    ISender sender,
    ILogger<ChargeCardHandler> logger)
    : ICommandHandler<ChargeCard, ChargeCardResult>
{
    // Few and quick: the fan is waiting for this answer.
    private const int InlineAttempts = 5;
    private const int RecordAttempts = 3;
    private static readonly TimeSpan InlineRetryDelay = TimeSpan.FromMilliseconds(200);

    public async ValueTask<ChargeCardResult> Handle(ChargeCard command, CancellationToken cancellationToken)
    {
        // Step 1: charge the card in Payments' own database, and commit.
        var declinedReason = await ChargeAsync(command, cancellationToken);
        if (declinedReason is not null)
        {
            // A declined card is a business answer, not a technical failure, so it is not retried.
            var reason = $"Payments could not charge the card: {declinedReason}";
            return new ChargeCardResult(ChargeCardOutcome.Failed, [], reason, [reason]);
        }

        List<string> steps = [$"Payments charged {Money.Format(command.Amount)}."];

        // Step 2: hand off to Ticketing, retrying inline with a short pause between attempts.
        var issue = new IssueTickets(command.OrderId, command.EventId, command.Quantity, command.SimulateTicketingFailures);
        for (var attempt = 1; attempt <= InlineAttempts; attempt++)
        {
            var tickets = await sender.TryIssueTicketsAsync(issue, logger, cancellationToken);
            if (tickets.Succeeded)
            {
                steps.Add(attempt == 1
                    ? $"Ticketing issued {tickets.TicketCodes.Count} ticket(s)."
                    : $"Ticketing issued {tickets.TicketCodes.Count} ticket(s) on attempt {attempt}.");
                return new ChargeCardResult(ChargeCardOutcome.Done, tickets.TicketCodes, null, steps);
            }

            steps.Add($"Ticketing failed (attempt {attempt} of {InlineAttempts}): {tickets.FailureReason} Nothing is rolled back.");
            if (attempt < InlineAttempts)
            {
                await Task.Delay(InlineRetryDelay, cancellationToken);
            }
        }

        // Step 3: Ticketing is still down. The seats stay held and the card stays charged. Payments records the
        // hand-off in its own database, and TicketHandoffRetrier keeps trying until Ticketing recovers.
        steps.Add(await TryRecordHandoffAsync(command)
            ? "Ticketing is still down. Payments will keep retrying in the background."
            : "Ticketing is still down, and Payments could not record the hand-off. Needs manual attention.");

        // The card is charged, so this is never Failed, even if recording the hand-off failed.
        // A Failed reply would make Inventory give the seats back and tell the fan they were not charged.
        return new ChargeCardResult(ChargeCardOutcome.Pending, [], null, steps);
    }

    // Returns null when the card is charged, or the reason it was not.
    private async Task<string?> ChargeAsync(ChargeCard command, CancellationToken cancellationToken)
    {
        var existing = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == command.OrderId, cancellationToken);
        if (existing is not null)
        {
            // Never charge the same order twice.
            return existing.Status == PaymentStatus.Charged
                ? null
                : existing.FailureReason ?? $"Payment is already {existing.Status}.";
        }

        // The order id is the idempotency key. The Payment row below is saved only after the charge, so if that save
        // fails and this step runs again, the check above finds nothing. The key makes the provider return the first
        // charge instead of charging the card a second time.
        var result = await provider.ChargeAsync($"charge-{command.OrderId}", command.PaymentToken, command.Amount, cancellationToken);

        db.Payments.Add(new Payment
        {
            OrderId = command.OrderId,
            Amount = command.Amount,
            Status = result.Succeeded ? PaymentStatus.Charged : PaymentStatus.Declined,
            ProviderReference = result.Reference,
            FailureReason = result.FailureReason,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Charge for order {OrderId}: {Outcome}", command.OrderId, result.Succeeded ? "charged" : "declined");
        return result.Succeeded ? null : result.FailureReason;
    }

    // Returns false when the hand-off could not be saved after a few tries. Nobody will retry Ticketing then,
    // so a person has to finish the order. The error is logged, and the reply says it needs attention.
    private async Task<bool> TryRecordHandoffAsync(ChargeCard command)
    {
        for (var attempt = 1; attempt <= RecordAttempts; attempt++)
        {
            try
            {
                await RecordHandoffAsync(command, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not record the Ticketing hand-off for order {OrderId} (attempt {Attempt})", command.OrderId, attempt);
                db.ChangeTracker.Clear(); // Forget the failed insert, so the next attempt starts clean.
                await Task.Delay(InlineRetryDelay * attempt);
            }
        }

        logger.LogCritical("Order {OrderId} is charged, but its Ticketing hand-off was not recorded. Needs manual attention.", command.OrderId);
        return false;
    }

    private async Task RecordHandoffAsync(ChargeCard command, CancellationToken cancellationToken)
    {
        if (await db.TicketHandoffs.AnyAsync(h => h.OrderId == command.OrderId, cancellationToken))
        {
            return; // Already recorded: recording is idempotent.
        }

        db.TicketHandoffs.Add(new TicketHandoff
        {
            OrderId = command.OrderId,
            EventId = command.EventId,
            Quantity = command.Quantity,
            SimulateTicketingFailures = command.SimulateTicketingFailures,
            Status = TicketHandoffStatus.Pending,
        });
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Ticketing is down for order {OrderId}. Retrying in the background.", command.OrderId);
    }
}
