using Ardalis.Result;
using Mediator;
using Microsoft.Extensions.Logging;
using TicketTide.Events.Contracts;
using TicketTide.Orders.Contracts;
using TicketTide.Orders.Internal.Data;
using TicketTide.Orders.Internal.Payments;
using TicketTide.SharedKernel.Data;
using TicketTide.Tickets.Contracts;

namespace TicketTide.Orders.Internal.UseCases.PlaceOrder;

/// <param name="SimulateTicketingFailure">A demo switch: the Tickets module crashes after writing the tickets.</param>
public sealed record PlaceOrderCommand(
    string EventId, int Quantity, string FanEmail, string PaymentToken, bool SimulateTicketingFailure = false)
    : ICommand<Result<OrderResponse>>;

/// <summary>
/// Buy tickets. Three modules change data, Events (the seats), Tickets (the tickets), and Orders (the order), and
/// this handler runs all of it in ONE database transaction. It either all happens or none of it does, so there's
/// nothing to compensate when a step fails, and no saga: the rollback undoes every module's work at once.
/// </summary>
/// <remarks>
/// The one thing a database transaction can't undo is the payment provider. So the card is charged last, once every
/// module's work has succeeded. If the card is declined, or anything before it fails, the card was never charged.
/// Only if saving the order or committing fails after the charge is the card refunded.
/// </remarks>
public sealed partial class PlaceOrderHandler(
    ISender sender,
    AfterCommitPublisher publisher,
    IUnitOfWork unitOfWork,
    IOrderRepository orders,
    IPaymentGateway payments,
    ILogger<PlaceOrderHandler> logger)
    : ICommandHandler<PlaceOrderCommand, Result<OrderResponse>>
{
    /// <returns>
    /// The order, which is Paid or Declined. Or NotFound for an unknown event, Invalid when there aren't enough
    /// seats, or Error when a step failed and the whole purchase was rolled back.
    /// </returns>
    public async ValueTask<Result<OrderResponse>> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var details = await sender.Send(new GetEventDetails(command.EventId), cancellationToken);
        if (!details.IsSuccess)
        {
            return Result.NotFound([.. details.Errors]);
        }

        // Fail fast, before starting a transaction. SellSeats checks again, atomically, in case someone else buys
        // the seats in the meantime.
        var evt = details.Value;
        if (command.Quantity > evt.SeatsAvailable)
        {
            return Result.Invalid(new ValidationError($"Not enough seats: {evt.Name} has {evt.SeatsAvailable} left."));
        }

        var order = Order.Place(evt, command.Quantity, command.FanEmail);
        IReadOnlyList<TicketDetails> tickets;
        PaymentResult? charge = null;
        var step = "Events";

        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                // 1. Events: take the seats off sale.
                var sold = await sender.Send(new SellSeats(evt.Id, command.Quantity), cancellationToken);
                if (!sold.IsSuccess)
                {
                    return sold; // Leaving the using block rolls back. Nothing was written yet anyway.
                }

                // 2. Tickets: issue one ticket per seat.
                step = "Tickets";
                var issued = await sender.Send(
                    new IssueTickets(order.Id, command.FanEmail, command.Quantity, command.SimulateTicketingFailure),
                    cancellationToken);
                if (!issued.IsSuccess)
                {
                    return Result.Error(string.Join(" ", issued.Errors));
                }

                tickets = issued.Value;

                // 3. The payment provider, last, because it's the one step the rollback can't undo. From here on,
                // the request's cancellation is ignored: once the card might be charged, the outcome must be recorded.
                step = "Payments";
                charge = await payments.ChargeAsync($"charge-{order.Id}", command.PaymentToken, order.Amount, CancellationToken.None);
                if (!charge.Succeeded)
                {
                    // Undo the seats and tickets in one go, then save just the declined order.
                    await transaction.RollbackAsync(CancellationToken.None);
                    order.MarkDeclined(charge.FailureReason ?? "The payment was declined.");
                    orders.Add(order);
                    var savedDeclined = await orders.SaveChangesAsync(CancellationToken.None);
                    return savedDeclined.IsSuccess ? OrderResponse.From(order, []) : savedDeclined;
                }

                // 4. Orders: save the paid order.
                step = "Orders";
                order.MarkPaid(charge.Reference!);
                orders.Add(order);
                var saved = await orders.SaveChangesAsync(CancellationToken.None);
                if (!saved.IsSuccess)
                {
                    throw new InvalidOperationException(string.Join(" ", saved.Errors));
                }

                // Every module's work becomes permanent at the same moment.
                step = "Commit";
                await transaction.CommitAsync(CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || charge is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                LogRolledBack(logger, ex, order.Id, step);

                // The only compensation in this sample, and only for the system outside the database.
                if (charge is { Succeeded: true })
                {
                    await payments.RefundAsync($"refund-{order.Id}", charge.Reference!, CancellationToken.None);
                }

                return Result.Error(
                    $"{step} failed, so the whole purchase was rolled back. Nothing about order {order.Id} was saved: " +
                    $"no seats were sold and no tickets were issued, and the card was " +
                    $"{(charge is { Succeeded: true } ? "refunded" : "never charged")}.");
            }
        }

        // After the commit, tell the other modules. What they do next (the Tickets module emails the fan) can't be
        // rolled back, so it must not happen until the order is permanent.
        await publisher.PublishAsync(new OrderPlaced(order.Id, order.EventName, order.FanEmail, order.Amount));

        return OrderResponse.From(order, tickets);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Placing order {OrderId} failed at {Step}. Rolled back every module's changes.")]
    private static partial void LogRolledBack(ILogger logger, Exception exception, Guid orderId, string step);
}
