using Mediator;

namespace TicketTide.Payments.Contracts;

/// <summary>
/// Charge the fan. The payment token comes from the payment provider's checkout widget,
/// so card data never touches TicketTide. Use <c>tok_declined</c> to simulate a declined card.
/// </summary>
public sealed record ChargeCard(Guid OrderId, decimal Amount, string PaymentToken) : ICommand<ChargeCardResult>;

public sealed record ChargeCardResult(bool Succeeded, string? FailureReason)
{
    public static ChargeCardResult Charged() => new(true, null);

    public static ChargeCardResult Declined(string reason) => new(false, reason);
}

/// <summary>Compensating command: refund a charge. Safe to call more than once.</summary>
public sealed record RefundCard(Guid OrderId) : ICommand;
