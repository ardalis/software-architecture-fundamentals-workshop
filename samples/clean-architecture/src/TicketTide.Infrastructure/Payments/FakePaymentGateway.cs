using System.Collections.Concurrent;
using TicketTide.Core.Interfaces;

namespace TicketTide.Infrastructure.Payments;

/// <summary>
/// A stand-in for a real provider such as Stripe. <c>tok_declined</c> is declined; every other token succeeds.
/// </summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    public const string DeclinedToken = "tok_declined";

    // Every answer, by idempotency key. A real provider keeps these on its side.
    private readonly ConcurrentDictionary<string, PaymentResult> _answers = new();

    public async Task<PaymentResult> ChargeAsync(string idempotencyKey, string paymentToken, decimal amount, CancellationToken cancellationToken)
    {
        if (_answers.TryGetValue(idempotencyKey, out var earlier))
        {
            return earlier; // Seen this key before: same answer, and the card is not charged again.
        }

        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken); // Pretend to call out over the network.

        var result = paymentToken == DeclinedToken
            ? new PaymentResult(false, null, "The card was declined.")
            : new PaymentResult(true, $"ch_{Guid.NewGuid():N}", null);
        return _answers.GetOrAdd(idempotencyKey, result);
    }

    public async Task<PaymentResult> RefundAsync(string idempotencyKey, string paymentReference, CancellationToken cancellationToken)
    {
        if (_answers.TryGetValue(idempotencyKey, out var earlier))
        {
            return earlier; // Seen this key before: the charge is not refunded twice.
        }

        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        return _answers.GetOrAdd(idempotencyKey, new PaymentResult(true, $"re_{Guid.NewGuid():N}", null));
    }
}
