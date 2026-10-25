namespace TicketTide.Checkout.Contracts;

// Events Checkout publishes over RabbitMQ. There is no orchestrator, so this event starts the saga
// and carries everything the services down the chain will need.

/// <summary>
/// A fan placed an order. Inventory reacts by holding the seats.
/// The payment token is for Payments and <paramref name="SimulateTicketingFailures"/> is for Ticketing:
/// Checkout never talks to them, so the events pass these along the chain.
/// </summary>
public sealed record OrderPlaced(
    Guid OrderId,
    string EventId,
    int Quantity,
    string PaymentToken,
    int SimulateTicketingFailures);
