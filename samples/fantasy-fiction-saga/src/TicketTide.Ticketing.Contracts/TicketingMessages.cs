namespace TicketTide.Ticketing.Contracts;

// Messages sent to Ticketing over RabbitMQ.

/// <summary>
/// Issue tickets for an order. Replies with <see cref="TicketsIssued"/> or <see cref="TicketingFailed"/>.
/// <paramref name="SimulatedFailures"/> makes Ticketing fail that many attempts for this order before it succeeds.
/// </summary>
public sealed record IssueTickets(Guid OrderId, string EventId, int Quantity, int SimulatedFailures);

/// <summary>Compensating command: cancel tickets so they cannot be scanned. Replies with <see cref="TicketsVoided"/>.</summary>
public sealed record VoidTickets(Guid OrderId);

// Replies Ticketing publishes.

public sealed record TicketsIssued(Guid OrderId, IReadOnlyList<string> TicketCodes);

public sealed record TicketingFailed(Guid OrderId, string Reason);

public sealed record TicketsVoided(Guid OrderId);
