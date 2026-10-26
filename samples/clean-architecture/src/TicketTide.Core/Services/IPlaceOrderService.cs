using Ardalis.Result;
using TicketTide.Core.OrderAggregate;

namespace TicketTide.Core.Services;

/// <summary>
/// The "place order" use case, as Web sees it. Web depends on this interface, not on the class that implements it,
/// so the composition root decides which implementation it gets.
/// </summary>
public interface IPlaceOrderService
{
    Task<Result<Order>> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken);
}
