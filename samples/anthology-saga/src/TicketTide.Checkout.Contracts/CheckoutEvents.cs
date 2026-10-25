namespace TicketTide.Checkout.Contracts;

// Events Checkout publishes over RabbitMQ.

/// <summary>
/// Checkout accepted an order. There is no orchestrator to ask later, so the event carries everything the
/// services downstream need: the seats for Inventory, the payment token for Payments, and (for the demo)
/// how many times Ticketing should fail.
/// </summary>
public sealed record OrderPlaced(Guid OrderId, string EventId, int Quantity, string PaymentToken, int SimulateTicketingFailures);
