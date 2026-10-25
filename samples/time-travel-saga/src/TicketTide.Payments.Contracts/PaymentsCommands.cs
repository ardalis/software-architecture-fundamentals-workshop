using Mediator;

namespace TicketTide.Payments.Contracts;

/// <summary>
/// The second link in the chain: charge the fan, then hand off to Ticketing.
/// The payment token comes from the payment provider's checkout widget, so card data never touches TicketTide.
/// Use <c>tok_declined</c> to simulate a declined card. Charging the same order twice never charges it twice.
/// </summary>
public sealed record ChargeCard(
    Guid OrderId,
    string EventId,
    int Quantity,
    decimal Amount,
    string PaymentToken,
    int SimulateTicketingFailures) : ICommand<ChargeCardResult>;

public enum ChargeCardOutcome
{
    /// <summary>The card is charged and the tickets are issued.</summary>
    Done,

    /// <summary>The card is charged. Ticketing is down, and Payments keeps retrying it in the background.</summary>
    Pending,

    /// <summary>The card was not charged.</summary>
    Failed,
}

/// <summary>The reply that travels back up the chain, with the steps Payments and Ticketing took.</summary>
public sealed record ChargeCardResult(
    ChargeCardOutcome Outcome,
    IReadOnlyList<string> TicketCodes,
    string? FailureReason,
    IReadOnlyList<string> Steps);
