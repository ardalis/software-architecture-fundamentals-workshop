using Mediator;

namespace TicketTide.Ticketing.Contracts;

/// <summary>
/// Issue tickets for an order. Issuing the same order twice returns the tickets already issued.
/// <paramref name="SimulatedFailures"/> makes Ticketing fail that many attempts for this order before it succeeds.
/// </summary>
public sealed record IssueTickets(Guid OrderId, string EventId, int Quantity, int SimulatedFailures)
    : ICommand<IssueTicketsResult>;

public sealed record IssueTicketsResult(bool Succeeded, IReadOnlyList<string> TicketCodes, string? FailureReason)
{
    public static IssueTicketsResult Issued(IReadOnlyList<string> ticketCodes) => new(true, ticketCodes, null);

    public static IssueTicketsResult Failed(string reason) => new(false, [], reason);
}
