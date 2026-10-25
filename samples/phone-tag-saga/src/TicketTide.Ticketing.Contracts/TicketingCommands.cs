using Mediator;

namespace TicketTide.Ticketing.Contracts;

/// <summary>
/// Issue tickets for an order. Ticketing is the last link in the chain, so it calls nobody else.
/// Issuing the same order twice returns the tickets already issued.
/// <paramref name="SimulatedFailures"/> makes Ticketing fail that many attempts for this order before it succeeds.
/// </summary>
public sealed record IssueTickets(Guid OrderId, string EventId, int Quantity, int SimulatedFailures)
    : ICommand<IssueTicketsResult>;

/// <summary>Ticketing's reply. <paramref name="Steps"/> is what Ticketing did, for the order's history.</summary>
public sealed record IssueTicketsResult(
    bool Succeeded,
    IReadOnlyList<string> TicketCodes,
    string? FailureReason,
    IReadOnlyList<string> Steps)
{
    public static IssueTicketsResult Issued(IReadOnlyList<string> ticketCodes) =>
        new(true, ticketCodes, null, [$"Ticketing issued {ticketCodes.Count} ticket(s)."]);

    public static IssueTicketsResult Failed(string reason)
    {
        var failure = $"Ticketing could not issue the tickets: {reason}";
        return new(false, [], failure, [failure]);
    }
}
