using FastEndpoints;
using FluentValidation;
using TicketTide.Core.OrderAggregate;
using TicketTide.Core.Services;

namespace TicketTide.Web.Orders;

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
/// Buy tickets: <c>201 Created</c> when the card is charged and the tickets are issued, or
/// <c>422 Unprocessable Entity</c> with the declined order.
/// </summary>
internal sealed class PlaceOrder(IPlaceOrderService service) : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets for an event.");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        var result = await service.PlaceOrderAsync(
            new PlaceOrderCommand(req.EventId, req.Quantity, req.FanEmail, req.PaymentToken), ct);
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToFailureResponse());
            return;
        }

        var order = result.Value;
        if (order.Status == OrderStatus.Declined)
        {
            await Send.ResponseAsync(OrderResponse.From(order), StatusCodes.Status422UnprocessableEntity, ct);
            return;
        }

        await Send.CreatedAtAsync<GetOrder>(new { orderId = order.Id }, OrderResponse.From(order), cancellation: ct);
    }
}
