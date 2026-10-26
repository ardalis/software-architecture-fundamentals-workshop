using Ardalis.Result;
using TicketTide.Core.Interfaces;
using TicketTide.Core.OrderAggregate;

namespace TicketTide.Core.Services;

/// <summary>The "refund order" use case: support refunds a paid order, and its seats go back on sale.</summary>
public sealed class RefundOrderService(
    IOrderRepository orders,
    IEventRepository events,
    IUnitOfWork unitOfWork,
    IPaymentGateway payments,
    IFanNotifier notifier) : IRefundOrderService
{
    /// <returns>
    /// The refunded order. Or NotFound for an unknown order, Invalid when the order isn't paid, or Conflict when
    /// another request refunded it first.
    /// </returns>
    public async Task<Result<Order>> RefundAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return Result.NotFound();
        }

        var refunded = order.Refund();
        if (!refunded.IsSuccess)
        {
            return refunded;
        }

        // The same idempotency key every time, so a retried or concurrent refund never refunds the card twice.
        var refund = await payments.RefundAsync($"refund-{order.Id}", order.PaymentReference!, cancellationToken);
        if (!refund.Succeeded)
        {
            return Result.Error(refund.FailureReason ?? "The payment provider could not refund the card.");
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (!saved.IsSuccess)
        {
            // Another request refunded this order first. The card was refunded once, by whichever request got there.
            return saved.Status == ResultStatus.Conflict
                ? Result.Conflict("This order was refunded by another request.")
                : saved;
        }

        // Only after the order is saved as refunded, so a failed save can't return the seats twice.
        await events.ReturnSeatsAsync(order.EventId, order.Quantity, cancellationToken);

        await notifier.SendRefundAsync(order, cancellationToken);
        return order;
    }
}
