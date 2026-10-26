using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.EmailSending.Contracts;
using TicketTide.Orders.Contracts;
using TicketTide.Tickets.Internal.Data;

namespace TicketTide.Tickets.Internal.Integrations;

/// <summary>
/// Emails the fan their tickets once the order commits. This runs after the transaction, because an email can't be
/// rolled back: if it ran inside, a later failure would roll back the order but not the email.
/// </summary>
public sealed class OrderPlacedHandler(TicketsDbContext db, ISender sender) : INotificationHandler<OrderPlaced>
{
    public async ValueTask Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        var codes = await db.Tickets.AsNoTracking()
            .Where(t => t.OrderId == notification.OrderId)
            .OrderBy(t => t.Number)
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);

        await sender.Send(
            new SendEmail(
                notification.FanEmail,
                $"Your tickets for {notification.EventName}",
                $"""
                Thanks for your order!

                Order: {notification.OrderId}
                Event: {notification.EventName}
                Tickets: {string.Join(", ", codes)}
                Total charged: {Money.Format(notification.Amount)}
                """),
            cancellationToken);
    }
}

/// <summary>Tells the fan their tickets no longer work, once the refund commits.</summary>
public sealed class OrderRefundedHandler(ISender sender) : INotificationHandler<OrderRefunded>
{
    public async ValueTask Handle(OrderRefunded notification, CancellationToken cancellationToken) =>
        await sender.Send(
            new SendEmail(
                notification.FanEmail,
                $"Your refund for {notification.EventName}",
                $"""
                Your order has been refunded, and its tickets no longer work.

                Order: {notification.OrderId}
                Refunded: {Money.Format(notification.Amount)}
                """),
            cancellationToken);
}
