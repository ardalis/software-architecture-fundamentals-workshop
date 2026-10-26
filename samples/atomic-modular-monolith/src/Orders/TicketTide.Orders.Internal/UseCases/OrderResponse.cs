using TicketTide.Orders.Internal.Data;
using TicketTide.Tickets.Contracts;

namespace TicketTide.Orders.Internal.UseCases;

/// <summary>The order as the API returns it: the Orders module's order, with the Tickets module's tickets.</summary>
public sealed record OrderResponse(
    Guid OrderId,
    string Status,
    string EventId,
    string EventName,
    string FanEmail,
    int Quantity,
    decimal Amount,
    string? FailureReason,
    IReadOnlyList<TicketResponse> Tickets)
{
    internal static OrderResponse From(Order order, IReadOnlyList<TicketDetails> tickets) => new(
        order.Id,
        order.Status.ToString(),
        order.EventId,
        order.EventName,
        order.FanEmail,
        order.Quantity,
        order.Amount,
        order.FailureReason,
        [.. tickets.Select(t => new TicketResponse(t.TicketId, t.Code, t.HolderEmail, t.Status))]);
}

public sealed record TicketResponse(Guid TicketId, string Code, string HolderEmail, string Status);
