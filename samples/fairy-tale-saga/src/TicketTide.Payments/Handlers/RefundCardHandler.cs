using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments.Handlers;

public sealed class RefundCardHandler(PaymentsDbContext db, IPaymentProvider provider, ILogger<RefundCardHandler> logger)
    : ICommandHandler<RefundCard>
{
    public async ValueTask<Unit> Handle(RefundCard command, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == command.OrderId, cancellationToken);
        if (payment is not { Status: PaymentStatus.Charged })
        {
            return Unit.Value; // Nothing to undo. Compensations must be idempotent.
        }

        // Same idea as the charge: if the save below fails and the refund runs again, the key stops a second refund.
        var result = await provider.RefundAsync($"refund-{payment.OrderId}", payment.ProviderReference!, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Refund failed for order {command.OrderId}: {result.FailureReason}");
        }

        payment.Status = PaymentStatus.Refunded;
        payment.ProviderReference = result.Reference;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Refunded {Amount} for order {OrderId}", payment.Amount, command.OrderId);
        return Unit.Value;
    }
}
