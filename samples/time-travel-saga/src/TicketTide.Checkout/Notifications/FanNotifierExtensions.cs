using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Data;

namespace TicketTide.Checkout.Notifications;

internal static class FanNotifierExtensions
{
    // The email is a side effect, not a saga step: if it fails, the order outcome does not change.
    // A pending order gets no email yet. The fan hears from us once the tickets are confirmed.
    public static async Task TryNotifyAsync(this IFanNotifier notifier, Order order, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            switch (order.Status)
            {
                case OrderStatus.Confirmed:
                    await notifier.SendConfirmationAsync(order, cancellationToken);
                    break;
                case OrderStatus.Failed:
                    await notifier.SendFailureAsync(order, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not email the fan about order {OrderId}", order.Id);
        }
    }
}
