using FastEndpoints;
using FluentValidation;
using TicketTide.Checkout.Data;
using Order = TicketTide.Checkout.Data.Order;

namespace TicketTide.Checkout.Endpoints;

public sealed class PlaceOrderRequest
{
    public string EventId { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public string FanEmail { get; init; } = string.Empty;

    /// <summary>A token from the payment provider. <c>tok_visa</c> succeeds, <c>tok_declined</c> is declined.</summary>
    public string PaymentToken { get; init; } = string.Empty;

    /// <summary>Demo only: how many times Ticketing should fail for this order before it succeeds.</summary>
    public int SimulateTicketingFailures { get; init; }
}

internal sealed class PlaceOrderValidator : Validator<PlaceOrderRequest>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.EventId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 8);
        RuleFor(x => x.FanEmail).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.PaymentToken).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SimulateTicketingFailures).InclusiveBetween(0, 10);
    }
}

public sealed record OrderResponse(
    Guid OrderId,
    string Status,
    string EventId,
    string? EventName,
    int Quantity,
    decimal? Amount,
    IReadOnlyList<string> TicketCodes,
    string? FailureReason,
    IReadOnlyList<string> History)
{
    internal static OrderResponse From(Order order) => new(
        order.Id,
        order.Status.ToString(),
        order.EventId,
        order.EventName,
        order.Quantity,
        order.Amount,
        order.TicketCodes,
        order.FailureReason,
        order.History);
}
