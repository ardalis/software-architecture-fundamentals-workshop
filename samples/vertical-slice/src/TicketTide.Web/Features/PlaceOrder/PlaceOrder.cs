using System.Security.Cryptography;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TicketTide.Web.Data;
using TicketTide.Web.Features.GetOrder;
using TicketTide.Web.Infrastructure;
using Order = TicketTide.Web.Data.Order;

namespace TicketTide.Web.Features.PlaceOrder;

public sealed class PlaceOrderRequest
{
    public string EventId { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public string FanEmail { get; init; } = string.Empty;

    /// <summary>A token from the payment provider. <c>tok_visa</c> succeeds, <c>tok_declined</c> is declined.</summary>
    public string PaymentToken { get; init; } = string.Empty;
}

internal sealed class PlaceOrderValidator : Validator<PlaceOrderRequest>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.EventId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 8);
        RuleFor(x => x.FanEmail).NotEmpty().EmailAddress().MaximumLength(254)
            .Must(email => MimeKit.MailboxAddress.TryParse(email, out _)).WithMessage("'Fan Email' is not an address we can send email to.");
        RuleFor(x => x.PaymentToken).NotEmpty().MaximumLength(100);
    }
}

/// <summary>
/// Buy tickets: check the seats, charge the card, sell the seats, and issue one ticket per seat.
/// <c>201 Created</c> when the tickets are issued, or <c>422 Unprocessable Entity</c> with the declined order.
/// Everything the feature does is here, top to bottom.
/// </summary>
internal sealed class PlaceOrderEndpoint(AppDbContext db, IPaymentGateway payments, IEmailSender email)
    : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets for an event.");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        var evt = await db.Events.FirstOrDefaultAsync(e => e.Id == req.EventId, ct);
        if (evt is null)
        {
            await Send.ResultAsync(TypedResults.Problem(statusCode: 404, detail: $"There is no event with id {req.EventId}."));
            return;
        }

        var seatsAvailable = evt.Capacity - evt.SeatsSold;
        if (req.Quantity > seatsAvailable)
        {
            await Send.ResultAsync(TypedResults.Problem(statusCode: 400, detail: $"Not enough seats: {evt.Name} has {seatsAvailable} left."));
            return;
        }

        var orderId = Guid.CreateVersion7();
        var amount = evt.Price * req.Quantity;
        var charge = await payments.ChargeAsync($"charge-{orderId}", req.PaymentToken, amount, ct);
        if (!charge.Succeeded)
        {
            var declined = NewOrder(orderId, evt, req, amount, OrderStatus.Declined);
            declined.FailureReason = charge.FailureReason ?? "The payment was declined.";
            db.Orders.Add(declined);
            await db.SaveChangesAsync(ct); // Nothing was charged, so a failure here is just a 500.
            await Send.ResponseAsync(OrderResponse.From(declined), StatusCodes.Status422UnprocessableEntity, ct);
            return;
        }

        evt.SeatsSold += req.Quantity;
        var order = NewOrder(orderId, evt, req, amount, OrderStatus.Paid);
        order.PaymentReference = charge.Reference;
        for (var i = 0; i < req.Quantity; i++)
        {
            order.Tickets.Add(new Ticket
            {
                OrderId = order.Id,
                Code = $"TT-{Convert.ToHexString(RandomNumberGenerator.GetBytes(5))}",
                HolderEmail = req.FanEmail,
                Status = TicketStatus.Paid,
            });
        }

        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // The card was charged, but the order wasn't saved, so give the money back. Usually another fan bought
            // seats for this event between our read and our save (a concurrency exception).
            await payments.RefundAsync($"refund-{orderId}", charge.Reference!, ct);
            if (ex is not DbUpdateConcurrencyException)
            {
                throw;
            }

            await Send.ResultAsync(TypedResults.Problem(statusCode: 409, detail: "Someone else just bought seats for this event. Please try again."));
            return;
        }

        await email.SendAsync(
            order.FanEmail,
            $"Your tickets for {order.EventName}",
            $"""
            Thanks for your order!

            Order: {order.Id}
            Event: {order.EventName}
            Tickets: {string.Join(", ", order.Tickets.Select(t => t.Code))}
            Total charged: {Money.Format(order.Amount)}
            """,
            ct);

        await Send.CreatedAtAsync<GetOrderEndpoint>(new { orderId = order.Id }, OrderResponse.From(order), cancellation: ct);
    }

    private static Order NewOrder(Guid id, Event evt, PlaceOrderRequest req, decimal amount, OrderStatus status) => new()
    {
        Id = id,
        EventId = evt.Id,
        EventName = evt.Name,
        FanEmail = req.FanEmail,
        Quantity = req.Quantity,
        Amount = amount,
        Status = status,
    };
}
