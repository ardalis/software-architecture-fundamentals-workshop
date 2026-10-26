using Mediator;

namespace TicketTide.Orders.Contracts;

// Orders runs the transaction, so no other module sends it commands. What it offers the rest of the system is these
// integration events, published only AFTER the transaction commits. A module that handles one does its work outside
// the transaction: if that work fails, the order still stands. The Tickets module handles both, to email the fan.

/// <summary>An order was paid, its seats were sold, and its tickets were issued, all in one committed transaction.</summary>
public sealed record OrderPlaced(Guid OrderId, string EventName, string FanEmail, decimal Amount) : INotification;

/// <summary>An order was refunded, its tickets stopped working, and its seats went back on sale.</summary>
public sealed record OrderRefunded(Guid OrderId, string EventName, string FanEmail, decimal Amount) : INotification;
