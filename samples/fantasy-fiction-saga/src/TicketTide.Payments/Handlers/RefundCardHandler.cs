using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments.Handlers;

internal sealed class RefundCardHandler(
    PaymentsDbContext db,
    IPaymentProvider provider,
    IMessageBus bus,
    ILogger<RefundCardHandler> logger) : IMessageHandler<RefundCard>
{
    public async Task HandleAsync(RefundCard message, MessageContext context, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == message.OrderId, cancellationToken);
        if (payment is null)
        {
            // The refund arrived before the charge (messages can overtake each other).
            // Record a cancelled payment, so a late ChargeCard for this order never charges the card.
            db.Payments.Add(new Payment
            {
                OrderId = message.OrderId,
                Status = PaymentStatus.Cancelled,
                FailureReason = "The order was cancelled before the card was charged.",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (payment.Status == PaymentStatus.Charged)
        {
            // Same idea as the charge: if the save below fails and the refund runs again, the key stops a second refund.
            var result = await provider.RefundAsync($"refund-{payment.OrderId}", payment.ProviderReference!, cancellationToken);
            if (!result.Succeeded)
            {
                // Throwing puts the message back on the queue, so the refund is retried.
                throw new InvalidOperationException($"Refund failed for order {message.OrderId}: {result.FailureReason}");
            }

            payment.Status = PaymentStatus.Refunded;
            payment.ProviderReference = result.Reference;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Refunded {Amount} for order {OrderId}", payment.Amount, message.OrderId);
        }

        // Declined, already refunded, or cancelled: nothing to undo. Compensations must be idempotent.
        await bus.PublishAsync(new CardRefunded(message.OrderId), cancellationToken);
    }
}
