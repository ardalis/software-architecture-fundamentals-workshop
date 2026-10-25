namespace TicketTide.Inventory.Contracts;

// Events Inventory publishes over RabbitMQ.

/// <summary>
/// Inventory held the seats. Payments reacts by charging the card. Inventory knows the price,
/// so it adds the amount; the rest is passed along for the services further down the chain.
/// </summary>
public sealed record SeatsHeld(
    Guid OrderId,
    string EventId,
    string EventName,
    int Quantity,
    decimal Amount,
    string PaymentToken,
    int SimulateTicketingFailures);

/// <summary>Inventory could not hold the seats (sold out, or no such event). Nothing happened, so nothing to undo.</summary>
public sealed record SeatsRejected(Guid OrderId, string Reason);

/// <summary>
/// Compensation: Inventory gave the seats back. It is the last compensation in the chain: Inventory only publishes
/// it once every service downstream of it has reported in, so Checkout can tell the fan.
/// <paramref name="Heard"/> lists the failure events Inventory heard, in the order they arrived.
/// </summary>
public sealed record SeatsReleased(Guid OrderId, string Reason, IReadOnlyList<string> Heard);
