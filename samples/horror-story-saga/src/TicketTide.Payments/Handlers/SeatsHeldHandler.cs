using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Inventory.Contracts;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments.Handlers;

/// <summary>The seats are held: charge the card, then publish Payment Received (for Ticketing) or Payment Declined.</summary>
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
            // Never charge the same order twice. Just publish the outcome again.
            switch (existing.Status)
            {
                case PaymentStatus.Charged:
                    await bus.PublishAsync(PaymentReceivedFor(message), cancellationToken);
                    break;
                case PaymentStatus.Declined:
                    await bus.PublishAsync(new PaymentDeclined(message.OrderId, existing.FailureReason!), cancellationToken);
                    break;
            }

            return; // Refunded or cancelled: the saga has already moved on.
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
            ? bus.PublishAsync(PaymentReceivedFor(message), cancellationToken)
            : bus.PublishAsync(new PaymentDeclined(message.OrderId, result.FailureReason!), cancellationToken));
    }

    // Pass along what Ticketing needs. Payments does not use the event or the failure count itself.
    private static PaymentReceived PaymentReceivedFor(SeatsHeld message) => new(
        message.OrderId,
        message.EventId,
        message.EventName,
        message.Quantity,
        message.Amount,
        message.SimulateTicketingFailures);
}
