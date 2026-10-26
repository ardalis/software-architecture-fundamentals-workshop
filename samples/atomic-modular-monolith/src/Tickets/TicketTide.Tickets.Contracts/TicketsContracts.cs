using Ardalis.Result;
using Mediator;

namespace TicketTide.Tickets.Contracts;

public sealed record TicketDetails(Guid TicketId, string Code, string HolderEmail, string Status);

/// <summary>
/// Issues one ticket per seat for an order, all held by <paramref name="HolderEmail"/>. Send it inside the caller's
/// unit of work, so the tickets disappear if anything after them fails.
/// </summary>
/// <param name="SimulateFailure">A demo switch: Ticketing writes the tickets, then crashes.</param>
public sealed record IssueTickets(Guid OrderId, string HolderEmail, int Quantity, bool SimulateFailure = false)
    : ICommand<Result<IReadOnlyList<TicketDetails>>>;

/// <summary>Every ticket in the order stops working.</summary>
public sealed record RefundTickets(Guid OrderId) : ICommand<Result<IReadOnlyList<TicketDetails>>>;

/// <summary>The order's tickets. Empty if it has none.</summary>
public sealed record GetTicketsForOrder(Guid OrderId) : IQuery<IReadOnlyList<TicketDetails>>;
