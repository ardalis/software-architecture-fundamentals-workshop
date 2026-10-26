using Ardalis.Result;
using Mediator;
using TicketTide.Events.Contracts;
using TicketTide.Orders.Contracts;
using TicketTide.Orders.Internal.Data;
using TicketTide.Orders.Internal.Payments;
using TicketTide.SharedKernel.Data;
using TicketTide.Tickets.Contracts;

namespace TicketTide.Orders.Internal.UseCases.RefundOrder;

public sealed record RefundOrderCommand(Guid OrderId) : ICommand<Result<OrderResponse>>;

/// <summary>
/// Support refunds a paid order. The order (Orders), its tickets (Tickets), and its seats (Events) all change in one
/// transaction, and the card is refunded last. If the payment provider can't refund it, the transaction rolls back
/// and nothing changed.
/// </summary>
public sealed class RefundOrderHandler(
    ISender sender,
    AfterCommitPublisher publisher,
    IUnitOfWork unitOfWork,
    IOrderRepository orders,
    IPaymentGateway payments)
    : ICommandHandler<RefundOrderCommand, Result<OrderResponse>>
{
    /// <returns>
    /// The refunded order. Or NotFound for an unknown order, Invalid when the order isn't paid, Conflict when another
    /// request refunded it first, or Error when the payment provider couldn't refund the card.
    /// </returns>
    public async ValueTask<Result<OrderResponse>> Handle(RefundOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.NotFound();
        }

        var refunded = order.Refund();
        if (!refunded.IsSuccess)
        {
            return refunded;
        }

        IReadOnlyList<TicketDetails> tickets;
        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            // 1. Orders: mark it refunded. The concurrency check stops a second refund of the same order.
            var saved = await orders.SaveChangesAsync(cancellationToken);
            if (!saved.IsSuccess)
            {
                return saved.Status == ResultStatus.Conflict
                    ? Result.Conflict("This order was refunded by another request.")
                    : saved;
            }

            // 2. Tickets: every ticket in the order stops working.
            var refundedTickets = await sender.Send(new RefundTickets(order.Id), cancellationToken);
            if (!refundedTickets.IsSuccess)
            {
                return Result.Error(string.Join(" ", refundedTickets.Errors));
            }

            tickets = refundedTickets.Value;

            // 3. Events: the seats go back on sale.
            var returned = await sender.Send(new ReturnSeats(order.EventId, order.Quantity), cancellationToken);
            if (!returned.IsSuccess)
            {
                return returned;
            }

            // 4. The payment provider, last. If it fails, leaving the using block rolls back steps 1 to 3.
            // The same idempotency key every time, so a retried refund never refunds the card twice.
            var refund = await payments.RefundAsync($"refund-{order.Id}", order.PaymentReference!, CancellationToken.None);
            if (!refund.Succeeded)
            {
                return Result.Error(refund.FailureReason ?? "The payment provider could not refund the card.");
            }

            await transaction.CommitAsync(CancellationToken.None);
        }

        // After the commit: the Tickets module emails the fan.
        await publisher.PublishAsync(new OrderRefunded(order.Id, order.EventName, order.FanEmail, order.Amount));

        return OrderResponse.From(order, tickets);
    }
}
