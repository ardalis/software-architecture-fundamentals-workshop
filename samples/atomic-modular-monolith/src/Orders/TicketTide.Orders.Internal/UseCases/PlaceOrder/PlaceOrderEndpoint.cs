using FastEndpoints;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http;
using TicketTide.Orders.Internal.UseCases.GetOrder;
using TicketTide.SharedKernel.Web;

namespace TicketTide.Orders.Internal.UseCases.PlaceOrder;

public sealed class PlaceOrderRequest
{
    public string EventId { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public string FanEmail { get; init; } = string.Empty;

    /// <summary>A token from the payment provider. <c>tok_visa</c> succeeds, <c>tok_declined</c> is declined.</summary>
    public string PaymentToken { get; init; } = string.Empty;

    /// <summary>A demo switch: Ticketing crashes after writing the tickets, and the whole purchase rolls back.</summary>
    public bool SimulateTicketingFailure { get; init; }
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
/// <c>422 Unprocessable Entity</c> with the declined order. HTTP only: the work is in <see cref="PlaceOrderHandler"/>.
/// </summary>
internal sealed class PlaceOrderEndpoint(ISender sender) : Endpoint<PlaceOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
        Summary(s => s.Summary = "Buy tickets for an event.");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        var result = await sender.Send(
            new PlaceOrderCommand(req.EventId, req.Quantity, req.FanEmail, req.PaymentToken, req.SimulateTicketingFailure), ct);
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToFailureResponse());
            return;
        }

        var order = result.Value;
        if (order.Status == nameof(Data.OrderStatus.Declined))
        {
            await Send.ResponseAsync(order, StatusCodes.Status422UnprocessableEntity, ct);
            return;
        }

        await Send.CreatedAtAsync<GetOrderEndpoint>(new { orderId = order.OrderId }, order, cancellation: ct);
    }
}
