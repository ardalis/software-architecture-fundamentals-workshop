using Ardalis.Result;
using Mediator;

namespace TicketTide.Events.Contracts;

/// <summary>An event, and how many seats it has left right now.</summary>
public sealed record EventDetails(string Id, string Name, decimal Price, int SeatsAvailable);

/// <summary>An event on sale. NotFound if there isn't one with this id.</summary>
public sealed record GetEventDetails(string EventId) : IQuery<Result<EventDetails>>;

/// <summary>
/// Takes seats off sale. Invalid if the event doesn't have that many left, and NotFound for an unknown event.
/// Send it inside the caller's unit of work, so the sale rolls back if anything after it fails.
/// </summary>
public sealed record SellSeats(string EventId, int Quantity) : ICommand<Result>;

/// <summary>Puts sold seats back on sale, such as for a refund.</summary>
public sealed record ReturnSeats(string EventId, int Quantity) : ICommand<Result>;
