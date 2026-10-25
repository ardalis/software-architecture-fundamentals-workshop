using TicketTide.Core.OrderAggregate;

namespace TicketTide.Web.Orders;

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
    internal static OrderResponse From(Order order) => new(
        order.Id,
        order.Status.ToString(),
        order.EventId,
        order.EventName,
        order.FanEmail,
        order.Quantity,
        order.Amount,
        order.FailureReason,
        [.. order.Tickets.Select(t => new TicketResponse(t.Id, t.Code, t.HolderEmail, t.Status.ToString()))]);
}

public sealed record TicketResponse(Guid TicketId, string Code, string HolderEmail, string Status);
