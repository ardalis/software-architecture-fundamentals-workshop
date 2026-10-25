using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TicketTide.Web.Data;
using TicketTide.Web.Infrastructure;

namespace TicketTide.Web.Features.RefundOrder;

public sealed record RefundOrderRequest(Guid OrderId);

/// <summary>Support refunds a paid order. Its tickets stop working, and its seats go back on sale.</summary>
internal sealed class RefundOrderEndpoint(AppDbContext db, IPaymentGateway payments, IEmailSender email)
    : Endpoint<RefundOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders/{orderId}/refund");
        AllowAnonymous();
        Summary(s => s.Summary = "Refund a paid order. Its tickets stop working and its seats go back on sale.");
    }

    public override async Task HandleAsync(RefundOrderRequest req, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Tickets).FirstOrDefaultAsync(o => o.Id == req.OrderId, ct);
        if (order is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (order.Status != OrderStatus.Paid)
        {
            await Send.ResultAsync(TypedResults.Problem(statusCode: 400, detail: $"Only a paid order can be refunded. This one is {order.Status}."));
            return;
        }

        order.Status = OrderStatus.Refunded;
        foreach (var ticket in order.Tickets)
        {
            ticket.Status = TicketStatus.Refunded;
        }

        // The same idempotency key every time, so a retried or concurrent refund never refunds the card twice.
        var refund = await payments.RefundAsync($"refund-{order.Id}", order.PaymentReference!, ct);
        if (!refund.Succeeded)
        {
            await Send.ResultAsync(TypedResults.Problem(statusCode: 500, detail: refund.FailureReason ?? "The payment provider could not refund the card."));
            return;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request refunded this order first. The card was refunded once, by whichever request got there.
            await Send.ResultAsync(TypedResults.Problem(statusCode: 409, detail: "This order was refunded by another request."));
            return;
        }

        // Put the seats back on sale only after the order is saved as refunded, so a failed save can't return them
        // twice. One UPDATE statement, so it can't conflict with fans buying seats for the same event.
        await db.Events
            .Where(e => e.Id == order.EventId && e.SeatsSold >= order.Quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsSold, e => e.SeatsSold - order.Quantity), ct);

        await email.SendAsync(
            order.FanEmail,
            $"Your refund for {order.EventName}",
            $"""
            Your order has been refunded, and its tickets no longer work.

            Order: {order.Id}
            Refunded: {Money.Format(order.Amount)}
            """,
            ct);

        await Send.OkAsync(OrderResponse.From(order), ct);
    }
}
