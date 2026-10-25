using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments.Handlers;

/// <summary>
/// When Inventory has held the seats, charge the card, then publish <see cref="PaymentReceived"/>
/// (or <see cref="PaymentDeclined"/>). Payments never hears from Checkout: everything it needs is in the event.
/// </summary>
internal sealed class SeatsHeldHandler(
    PaymentsDbContext db,
    IPaymentProvider provider,
    IMessageBus bus,
    ILogger<SeatsHeldHandler> logger) : IMessageHandler<SeatsHeld>
{
    public async Task HandleAsync(SeatsHeld message, MessageContext context, CancellationToken cancellationToken)
    {
        var existing = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == message.OrderId, cancellationToken);
        if (existing is not null)
        {
            // Never charge the same order twice. Just publish the event again.
            await PublishAsync(message, existing.Status == PaymentStatus.Charged, existing.FailureReason, cancellationToken);
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
        await PublishAsync(message, result.Succeeded, result.FailureReason, cancellationToken);
    }

    private Task PublishAsync(SeatsHeld message, bool charged, string? failureReason, CancellationToken cancellationToken) =>
        charged
            ? bus.PublishAsync(
                new PaymentReceived(message.OrderId, message.EventId, message.Quantity, message.Amount, message.SimulateTicketingFailures),
                cancellationToken)
            : bus.PublishAsync(new PaymentDeclined(message.OrderId, failureReason ?? "The card was declined."), cancellationToken);
}
