using Ardalis.Result;
using TicketTide.Core.OrderAggregate;

namespace TicketTide.Core.Services;

/// <summary>The "refund order" use case, as Web sees it.</summary>
public interface IRefundOrderService
{
    Task<Result<Order>> RefundAsync(Guid orderId, CancellationToken cancellationToken);
}
