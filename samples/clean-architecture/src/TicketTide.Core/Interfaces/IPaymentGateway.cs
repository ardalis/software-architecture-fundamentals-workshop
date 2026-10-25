namespace TicketTide.Core.Interfaces;

/// <summary>
/// The external payment provider. TicketTide only ever sees tokens, never card numbers. Every call takes an
/// idempotency key, so sending the same request twice doesn't move money twice.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(string idempotencyKey, string paymentToken, decimal amount, CancellationToken cancellationToken);

    Task<PaymentResult> RefundAsync(string idempotencyKey, string paymentReference, CancellationToken cancellationToken);
}

public sealed record PaymentResult(bool Succeeded, string? Reference, string? FailureReason);
