using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Contracts;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Notifications;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// The data catches up. Payments kept retrying Ticketing after the fan got <c>202 Accepted</c>, and now the
/// tickets are issued, so Checkout confirms the pending order and emails the fan.
/// </summary>
public sealed class TicketsIssuedHandler(CheckoutDbContext db, IFanNotifier notifier, ILogger<TicketsIssuedHandler> logger)
    : ICommandHandler<TicketsIssued, TicketsIssuedResult>
{
    public async ValueTask<TicketsIssuedResult> Handle(TicketsIssued command, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Tickets were issued for unknown order {OrderId}", command.OrderId);
            return new TicketsIssuedResult(Accepted: true); // Nothing to confirm, and retrying will not change that.
        }

        switch (order.Status)
        {
            case OrderStatus.Confirmed:
                return new TicketsIssuedResult(Accepted: true); // Already done: confirming is idempotent.

            case OrderStatus.Placed:
                // The chain's own reply has not reached Checkout yet. Ask Payments to tell us again later,
                // so the two writes to this order cannot overwrite each other.
                return new TicketsIssuedResult(Accepted: false);

            case OrderStatus.Failed:
                // Only possible if a module crashed on the way back up the chain. A person has to sort it out.
                logger.LogWarning("Tickets were issued for failed order {OrderId}. Needs manual attention.", order.Id);
                return new TicketsIssuedResult(Accepted: true);
        }

        order.TicketCodes = [.. command.TicketCodes];
        order.Note($"Payments retried in the background, and Ticketing issued {command.TicketCodes.Count} ticket(s).");
        order.MoveTo(OrderStatus.Confirmed, "Tickets confirmed.");
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderId} confirmed after a background retry", order.Id);
        await notifier.TryNotifyAsync(order, logger, CancellationToken.None);
        return new TicketsIssuedResult(Accepted: true);
    }
}
