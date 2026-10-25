using Mediator;

namespace TicketTide.Inventory.Contracts;

// Messages sent to Inventory over RabbitMQ.

/// <summary>Hold seats for an order. Replies with <see cref="SeatsHeld"/> or <see cref="SeatsRejected"/>.</summary>
public sealed record HoldSeats(Guid OrderId, string EventId, int Quantity);

/// <summary>Compensating command: give held seats back. Replies with <see cref="SeatsReleased"/>.</summary>
public sealed record ReleaseSeats(Guid OrderId);

// Replies Inventory publishes.

public sealed record SeatsHeld(Guid OrderId);

public sealed record SeatsRejected(Guid OrderId, string Reason);

public sealed record SeatsReleased(Guid OrderId);

/// <summary>
/// A synchronous, in-process query. Checkout prices the order before the saga starts,
/// because Payments needs the amount at the same moment Inventory gets the hold request.
/// </summary>
public sealed record GetEventPrice(string EventId) : IQuery<EventPrice?>;

public sealed record EventPrice(string EventId, string EventName, decimal Price);
