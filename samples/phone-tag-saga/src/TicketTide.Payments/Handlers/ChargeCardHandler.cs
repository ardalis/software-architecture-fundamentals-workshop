using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Payments.Handlers;

/// <summary>
/// Payments' link in the Phone Tag chain: charge the card, call Ticketing and wait for its answer,
/// and refund the card if Ticketing failed. Payments knows only the next step (Ticketing) and its own undo step.
/// </summary>
public sealed class ChargeCardHandler(
    PaymentsDbContext db,
    IPaymentProvider provider,
    ISender sender,
    ILogger<ChargeCardHandler> logger)
    : ICommandHandler<ChargeCard, ChargeCardResult>
{
    private const int CompensationAttempts = 3;

    public async ValueTask<ChargeCardResult> Handle(ChargeCard command, CancellationToken cancellationToken)
    {
        // This module's own step: charge the card.
        var failureReason = await ChargeAsync(command, cancellationToken);
        if (failureReason is not null)
        {
            return ChargeCardResult.Declined(failureReason);
        }

        var steps = new List<string> { $"Payments charged {Money.Format(command.Amount)}." };

        // The next step: Payments calls Ticketing directly and waits for its answer.
        var tickets = await IssueTicketsAsync(command, cancellationToken);
        steps.AddRange(tickets.Steps);
        if (tickets.Succeeded)
        {
            return ChargeCardResult.Completed(tickets.TicketCodes, steps);
        }

        // Ticketing failed, and the saga is atomic: undo this module's step before answering Inventory.
        if (await RefundWithRetriesAsync(command.OrderId))
        {
            steps.Add("Payments refunded the card.");
            return ChargeCardResult.Undone(tickets.FailureReason!, steps, needsAttention: false);
        }

        steps.Add($"Payments could not refund the card after {CompensationAttempts} attempts. Needs manual attention.");
        return ChargeCardResult.Undone(tickets.FailureReason!, steps, needsAttention: true);
    }

    // Returns null when the card is charged, or why it was not.
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
        return result.Succeeded ? null : result.FailureReason!;
    }

    // An exception from Ticketing is treated like a failed step, so the card is refunded too.
    // The details are logged, not shown to the fan.
    private async Task<IssueTicketsResult> IssueTicketsAsync(ChargeCard command, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.Send(
                new IssueTickets(command.OrderId, command.EventId, command.Quantity, command.SimulateTicketingFailures),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ticketing failed for order {OrderId}", command.OrderId);
            return IssueTicketsResult.Failed("Ticketing is unavailable.");
        }
    }

    private async Task<bool> RefundWithRetriesAsync(Guid orderId)
    {
        for (var attempt = 1; attempt <= CompensationAttempts; attempt++)
        {
            try
            {
                await RefundAsync(orderId, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refund failed for order {OrderId} (attempt {Attempt})", orderId, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
            }
        }

        return false;
    }

    // The compensating action. It lives here, inside Payments, because no other module knows how to undo a charge.
    private async Task RefundAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);
        if (payment is not { Status: PaymentStatus.Charged })
        {
            return; // Nothing to undo. Compensations must be idempotent.
        }

        // Same idea as the charge: if the save below fails and the refund runs again, the key stops a second refund.
        var result = await provider.RefundAsync($"refund-{payment.OrderId}", payment.ProviderReference!, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Refund failed for order {orderId}: {result.FailureReason}");
        }

        payment.Status = PaymentStatus.Refunded;
        payment.ProviderReference = result.Reference;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Refunded {Amount} for order {OrderId}", payment.Amount, orderId);
    }
}
