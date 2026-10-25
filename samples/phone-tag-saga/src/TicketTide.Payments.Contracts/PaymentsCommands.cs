using Mediator;

namespace TicketTide.Payments.Contracts;

/// <summary>
/// Charge the fan, then pass the purchase on to Ticketing and wait for its answer.
/// The payment token comes from the payment provider's checkout widget, so card data never touches TicketTide.
/// Use <c>tok_declined</c> to simulate a declined card.
/// </summary>
/// <remarks>
/// There is no orchestrator, so the command carries everything the rest of the chain needs:
/// <paramref name="EventId"/>, <paramref name="Quantity"/>, and <paramref name="SimulateTicketingFailures"/> are
/// only passed on to Ticketing.
/// </remarks>
public sealed record ChargeCard(
    Guid OrderId,
    decimal Amount,
    string PaymentToken,
    string EventId,
    int Quantity,
    int SimulateTicketingFailures) : ICommand<ChargeCardResult>;

/// <summary>
/// Payments' reply, after Ticketing has answered and Payments has undone its own step if needed.
/// <paramref name="Steps"/> is what Payments and Ticketing did, in order.
/// <paramref name="NeedsAttention"/> is true when a step further down the chain could not be undone.
/// </summary>
public sealed record ChargeCardResult(
    bool Succeeded,
    IReadOnlyList<string> TicketCodes,
    string? FailureReason,
    IReadOnlyList<string> Steps,
    bool NeedsAttention)
{
    public static ChargeCardResult Completed(IReadOnlyList<string> ticketCodes, IReadOnlyList<string> steps) =>
        new(true, ticketCodes, null, steps, false);

    public static ChargeCardResult Declined(string reason)
    {
        var failure = $"Payments could not charge the card: {reason}";
        return new(false, [], failure, [failure], false);
    }

    /// <summary>A later step failed, and Payments refunded the card (or tried to).</summary>
    public static ChargeCardResult Undone(string failureReason, IReadOnlyList<string> steps, bool needsAttention) =>
        new(false, [], failureReason, steps, needsAttention);
}
