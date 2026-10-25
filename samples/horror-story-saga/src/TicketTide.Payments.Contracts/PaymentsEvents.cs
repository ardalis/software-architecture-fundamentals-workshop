namespace TicketTide.Payments.Contracts;

// Events Payments publishes over RabbitMQ.

/// <summary>Payments charged the card. Ticketing reacts by issuing the tickets.</summary>
public sealed record PaymentReceived(
    Guid OrderId,
    string EventId,
    string EventName,
    int Quantity,
    decimal Amount,
    int SimulateTicketingFailures);

/// <summary>The card was declined. Inventory reacts by releasing the seats.</summary>
public sealed record PaymentDeclined(Guid OrderId, string Reason);

/// <summary>Compensation: Payments refunded the card (or there was nothing to refund).</summary>
public sealed record CardRefunded(Guid OrderId);
