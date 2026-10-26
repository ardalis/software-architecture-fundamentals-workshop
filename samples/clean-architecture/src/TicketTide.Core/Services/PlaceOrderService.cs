using Ardalis.Result;
using TicketTide.Core.Interfaces;
using TicketTide.Core.OrderAggregate;

namespace TicketTide.Core.Services;

public sealed record PlaceOrderCommand(string EventId, int Quantity, string FanEmail, string PaymentToken);

/// <summary>
/// The "place order" use case: check the seats, charge the card, sell the seats, and issue the tickets.
/// It coordinates the aggregates and the outside world. The rules themselves are on <see cref="Order"/> and
/// <see cref="EventAggregate.Event"/>.
/// </summary>
public sealed class PlaceOrderService(
    IEventRepository events,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    IPaymentGateway payments,
    IFanNotifier notifier) : IPlaceOrderService
{
    /// <returns>
    /// The order, which is <see cref="OrderStatus.Paid"/> or <see cref="OrderStatus.Declined"/>. Or NotFound for an
    /// unknown event, Invalid when there aren't enough seats, Conflict when another fan bought them first, or Error
    /// when the order couldn't be saved (the card is refunded).
    /// </returns>
    public async Task<Result<Order>> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var evt = await events.GetByIdAsync(command.EventId, cancellationToken);
        if (evt is null)
        {
            return Result.NotFound($"There is no event with id {command.EventId}.");
        }

        var available = evt.EnsureSeatsAvailable(command.Quantity);
        if (!available.IsSuccess)
        {
            return available;
        }

        var order = Order.Place(evt, command.Quantity, command.FanEmail);
        var charge = await payments.ChargeAsync($"charge-{order.Id}", command.PaymentToken, order.Amount, cancellationToken);
        if (!charge.Succeeded)
        {
            order.MarkDeclined(charge.FailureReason ?? "The payment was declined.");
            orders.Add(order);
            var savedDeclined = await unitOfWork.SaveChangesAsync(cancellationToken);
            return savedDeclined.IsSuccess ? order : savedDeclined;
        }

        evt.SellSeats(command.Quantity);
        order.MarkPaid(charge.Reference!);
        orders.Add(order);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (!saved.IsSuccess)
        {
            // The card was charged, but the order wasn't saved, so give the money back. Usually another fan bought
            // seats for this event between our read and our save.
            await payments.RefundAsync($"refund-{order.Id}", charge.Reference!, cancellationToken);
            return saved.Status == ResultStatus.Conflict
                ? Result.Conflict("Someone else just bought seats for this event. Please try again.")
                : saved;
        }

        await notifier.SendTicketsAsync(order, cancellationToken);
        return order;
    }
}
