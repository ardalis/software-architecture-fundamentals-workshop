using Mediator;

namespace TicketTide.Checkout.Contracts;

/// <summary>
/// Tell Checkout that the tickets for a pending order were issued after all.
/// Payments sends this when its background retries finally get through to Ticketing.
/// Telling Checkout twice is safe.
/// </summary>
public sealed record TicketsIssued(Guid OrderId, IReadOnlyList<string> TicketCodes) : ICommand<TicketsIssuedResult>;

/// <summary><c>Accepted</c> is false when Checkout is not ready for the news yet, so the sender should try again later.</summary>
public sealed record TicketsIssuedResult(bool Accepted);
