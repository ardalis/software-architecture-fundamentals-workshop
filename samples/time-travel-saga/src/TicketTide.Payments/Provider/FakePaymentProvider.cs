using System.Collections.Concurrent;

namespace TicketTide.Payments.Provider;

/// <summary>
/// The external payment provider. TicketTide only ever sees tokens, never card numbers.
/// Every call takes an idempotency key, like Stripe's <c>Idempotency-Key</c> header. Sending the same key again
/// returns the first answer instead of moving money again, so a retried or redelivered step is safe.
/// </summary>
public interface IPaymentProvider
{
    Task<ProviderResult> ChargeAsync(string idempotencyKey, string paymentToken, decimal amount, CancellationToken cancellationToken);
}

public sealed record ProviderResult(bool Succeeded, string? Reference, string? FailureReason);

/// <summary>
/// A stand-in for a real provider such as Stripe. <c>tok_declined</c> is declined; every other token succeeds.
/// </summary>
internal sealed class FakePaymentProvider : IPaymentProvider
{
    public const string DeclinedToken = "tok_declined";

    // Every answer, by idempotency key. A real provider keeps these on its side (Stripe keeps them for 24 hours).
    // This only works because the provider is a singleton.
    private readonly ConcurrentDictionary<string, ProviderResult> _answers = new();

    public async Task<ProviderResult> ChargeAsync(string idempotencyKey, string paymentToken, decimal amount, CancellationToken cancellationToken)
    {
        if (_answers.TryGetValue(idempotencyKey, out var earlier))
        {
            return earlier; // Seen this key before: same answer, and the card is not charged again.
        }

        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken); // Pretend to call out over the network.

        var result = paymentToken == DeclinedToken
            ? new ProviderResult(false, null, "The card was declined.")
            : new ProviderResult(true, $"ch_{Guid.NewGuid():N}", null);
        return _answers.GetOrAdd(idempotencyKey, result); // If two calls race, both get the first answer.
    }
}
