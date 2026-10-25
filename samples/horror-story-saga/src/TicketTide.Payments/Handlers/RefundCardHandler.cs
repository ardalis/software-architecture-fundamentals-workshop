using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Payments.Handlers;

/// <summary>
/// Payments' compensation. Ticketing failed, so the card must be refunded. Nobody tells Payments to do this:
/// it hears Ticketing's failure event itself, and then publishes Card Refunded for Inventory.
/// </summary>
internal sealed class RefundCardHandler(
    PaymentsDbContext db,
    IPaymentProvider provider,
    IMessageBus bus,
    ILogger<RefundCardHandler> logger) : IMessageHandler<TicketIssueFailed>
{
    public async Task HandleAsync(TicketIssueFailed message, MessageContext context, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == message.OrderId, cancellationToken);
        if (payment is null)
        {
            // Ticketing only runs after a charge, so this should not happen. If it does, record a cancelled
            // payment, so a late Seats Held for this order never charges the card.
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

        // Already refunded or cancelled: nothing to undo. Compensations must be idempotent.
        await bus.PublishAsync(new CardRefunded(message.OrderId), cancellationToken);
    }
}
