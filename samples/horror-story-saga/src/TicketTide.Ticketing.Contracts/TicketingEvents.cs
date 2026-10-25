namespace TicketTide.Ticketing.Contracts;

// Events Ticketing publishes over RabbitMQ.

/// <summary>The tickets are ready. Checkout reacts by confirming the order.</summary>
public sealed record TicketsIssued(Guid OrderId, string EventName, decimal Amount, IReadOnlyList<string> TicketCodes);

/// <summary>
/// Ticketing could not issue the tickets. The saga is atomic, so every step before this one must be undone.
/// There is no orchestrator to do it, so this one event goes to every service upstream: Payments and Inventory.
/// </summary>
public sealed record TicketIssueFailed(Guid OrderId, string Reason);
