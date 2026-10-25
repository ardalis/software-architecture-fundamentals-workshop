using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments.Handlers;

internal sealed class ChargeCardHandler(
    PaymentsDbContext db,
    IPaymentProvider provider,
    IMessageBus bus,
    ILogger<ChargeCardHandler> logger) : IMessageHandler<ChargeCard>
{
    public async Task HandleAsync(ChargeCard message, MessageContext context, CancellationToken cancellationToken)
    {
        var existing = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == message.OrderId, cancellationToken);
        if (existing is not null)
        {
            // Never charge the same order twice. Just repeat the reply.
            await (existing.Status == PaymentStatus.Charged
                ? bus.PublishAsync(new CardCharged(message.OrderId), cancellationToken)
                : bus.PublishAsync(new CardDeclined(message.OrderId, existing.FailureReason ?? $"Payment is {existing.Status}."), cancellationToken));
            return;
        }

        // The order id is the idempotency key. The Payment row below is saved only after the charge, so if that save
        // fails and this step runs again, the check above finds nothing. The key makes the provider return the first
        // charge instead of charging the card a second time.
        var result = await provider.ChargeAsync($"charge-{message.OrderId}", message.PaymentToken, message.Amount, cancellationToken);

        db.Payments.Add(new Payment
        {
            OrderId = message.OrderId,
            Amount = message.Amount,
            Status = result.Succeeded ? PaymentStatus.Charged : PaymentStatus.Declined,
            ProviderReference = result.Reference,
            FailureReason = result.FailureReason,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Charge for order {OrderId}: {Outcome}", message.OrderId, result.Succeeded ? "charged" : "declined");
        await (result.Succeeded
            ? bus.PublishAsync(new CardCharged(message.OrderId), cancellationToken)
            : bus.PublishAsync(new CardDeclined(message.OrderId, result.FailureReason!), cancellationToken));
    }
}
