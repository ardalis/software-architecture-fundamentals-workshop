namespace TicketTide.Inventory.Contracts;

// Events Inventory publishes over RabbitMQ.

/// <summary>
/// Inventory held the seats and priced the order. Payments reacts by charging the card, so the event
/// passes on what Payments and Ticketing need.
/// </summary>
public sealed record SeatsHeld(
    Guid OrderId,
    string EventId,
    string EventName,
    int Quantity,
    decimal Amount,
    string PaymentToken,
    int SimulateTicketingFailures);

/// <summary>A business "no": the event does not exist or is sold out. The saga ends here.</summary>
public sealed record SeatsRejected(Guid OrderId, string Reason);

/// <summary>Inventory gave the seats back, because the payment was declined.</summary>
public sealed record SeatsReleased(Guid OrderId);
