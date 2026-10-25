namespace TicketTide.Ticketing.Contracts;

// Events Ticketing publishes over RabbitMQ.

/// <summary>Ticketing issued the tickets. Checkout reacts by confirming the order and emailing the fan.</summary>
public sealed record TicketsIssued(Guid OrderId, IReadOnlyList<string> TicketCodes);
