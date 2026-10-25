using Mediator;

namespace TicketTide.Inventory.Contracts;

/// <summary>
/// The first link in the chain: hold seats for an order, then hand off to Payments.
/// The command carries what the later steps need (the payment token and the demo's failure injection),
/// because Checkout only ever talks to Inventory. Holding the same order twice reuses the existing hold.
/// </summary>
public sealed record HoldSeats(Guid OrderId, string EventId, int Quantity, string PaymentToken, int SimulateTicketingFailures)
    : ICommand<HoldSeatsResult>;

public enum HoldSeatsOutcome
{
    /// <summary>Every step down the chain is done: the tickets are issued.</summary>
    Done,

    /// <summary>The seats are held and the card is charged, but the tickets are still being issued.</summary>
    Pending,

    /// <summary>The purchase failed. Nothing is held or charged.</summary>
    Failed,
}

/// <summary>
/// The reply that travels back up the chain. <see cref="Steps"/> lists what each module did, in order,
/// so Checkout can record the order's history without knowing how the chain is built.
/// </summary>
public sealed record HoldSeatsResult(
    HoldSeatsOutcome Outcome,
    string? EventName,
    decimal? Amount,
    IReadOnlyList<string> TicketCodes,
    string? FailureReason,
    IReadOnlyList<string> Steps);
