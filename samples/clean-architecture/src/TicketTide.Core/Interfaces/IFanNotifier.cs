using TicketTide.Core.OrderAggregate;

namespace TicketTide.Core.Interfaces;

/// <summary>Tells fans what happened to their orders.</summary>
public interface IFanNotifier
{
    Task SendTicketsAsync(Order order, CancellationToken cancellationToken);

    Task SendRefundAsync(Order order, CancellationToken cancellationToken);
}
