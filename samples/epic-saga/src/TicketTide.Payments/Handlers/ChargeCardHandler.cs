using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments.Handlers;

public sealed class ChargeCardHandler(PaymentsDbContext db, IPaymentProvider provider, ILogger<ChargeCardHandler> logger)
    : ICommandHandler<ChargeCard, ChargeCardResult>
{
    public async ValueTask<ChargeCardResult> Handle(ChargeCard command, CancellationToken cancellationToken)
    {
        var existing = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == command.OrderId, cancellationToken);
        if (existing is not null)
        {
            // Never charge the same order twice.
            return existing.Status == PaymentStatus.Charged
                ? ChargeCardResult.Charged()
                : ChargeCardResult.Declined(existing.FailureReason ?? $"Payment is already {existing.Status}.");
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
        return result.Succeeded ? ChargeCardResult.Charged() : ChargeCardResult.Declined(result.FailureReason!);
    }
}
