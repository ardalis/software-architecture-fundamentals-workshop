namespace TicketTide.Payments.Contracts;

// Events Payments publishes over RabbitMQ.

/// <summary>Payments charged the card. Ticketing reacts by issuing the tickets.</summary>
public sealed record PaymentReceived(Guid OrderId, string EventId, int Quantity, decimal Amount, int SimulateTicketingFailures);

/// <summary>A business "no": the card was declined. Inventory reacts by releasing the seats.</summary>
public sealed record PaymentDeclined(Guid OrderId, string Reason);
