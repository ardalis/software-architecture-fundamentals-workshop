using Mediator;

namespace TicketTide.Inventory.Contracts;

/// <summary>
/// Hold seats for an order, then pass the purchase on to Payments and wait for its answer.
/// Holding the same order twice reuses the existing hold.
/// </summary>
/// <remarks>
/// There is no orchestrator, so the command carries everything the rest of the chain needs:
/// <paramref name="PaymentToken"/> is for Payments, and <paramref name="SimulateTicketingFailures"/> is for Ticketing.
/// </remarks>
public sealed record HoldSeats(
    Guid OrderId,
    string EventId,
    int Quantity,
    string PaymentToken,
    int SimulateTicketingFailures) : ICommand<HoldSeatsResult>;

/// <summary>
/// Inventory's reply: the final answer for the whole chain.
/// <paramref name="Steps"/> is what Inventory, Payments, and Ticketing did, in order.
/// <paramref name="NeedsAttention"/> is true when a step could not be undone.
/// </summary>
public sealed record HoldSeatsResult(
    bool Succeeded,
    string? EventName,
    decimal? Amount,
    IReadOnlyList<string> TicketCodes,
    string? FailureReason,
    IReadOnlyList<string> Steps,
    bool NeedsAttention)
{
    public static HoldSeatsResult Completed(
        string eventName, decimal amount, IReadOnlyList<string> ticketCodes, IReadOnlyList<string> steps) =>
        new(true, eventName, amount, ticketCodes, null, steps, false);

    public static HoldSeatsResult Rejected(string reason)
    {
        var failure = $"Inventory could not hold the seats: {reason}";
        return new(false, null, null, [], failure, [failure], false);
    }

    /// <summary>A later step failed, and Inventory released the seats (or tried to).</summary>
    public static HoldSeatsResult Undone(
        string eventName, decimal amount, string failureReason, IReadOnlyList<string> steps, bool needsAttention) =>
        new(false, eventName, amount, [], failureReason, steps, needsAttention);
}
