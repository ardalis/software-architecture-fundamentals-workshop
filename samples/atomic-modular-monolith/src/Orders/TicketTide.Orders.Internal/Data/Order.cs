using Ardalis.Result;
using TicketTide.Events.Contracts;

namespace TicketTide.Orders.Internal.Data;

public enum OrderStatus
{
    Placed,
    Paid,
    Declined,
    Refunded,
}

/// <summary>
/// A fan's order for tickets to one event. The order's own rules live here. Its seats belong to the Events module
/// and its tickets to the Tickets module: the order refers to them by id only.
/// </summary>
public sealed class Order
{
    // For EF Core.
    private Order()
    {
    }

    private Order(EventDetails evt, int quantity, string fanEmail)
    {
        EventId = evt.Id;
        EventName = evt.Name;
        FanEmail = fanEmail;
        Quantity = quantity;
        Amount = evt.Price * quantity;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string EventId { get; private set; } = string.Empty;

    public string EventName { get; private set; } = string.Empty;

    public string FanEmail { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal Amount { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Placed;

    /// <summary>The payment provider's reference for the charge, needed to refund it.</summary>
    public string? PaymentReference { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optimistic concurrency token, so two requests can't both refund the same order. A new value on every change,
    /// set here rather than by the database, so it works the same on SQL Server and on SQLite in the tests.
    /// </summary>
    public Guid Version { get; private set; } = Guid.NewGuid();

    public static Order Place(EventDetails evt, int quantity, string fanEmail) => new(evt, quantity, fanEmail);

    public void MarkPaid(string paymentReference)
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOperationException($"Only a placed order can be paid. This one is {Status}.");
        }

        Status = OrderStatus.Paid;
        PaymentReference = paymentReference;
        Version = Guid.NewGuid();
    }

    public void MarkDeclined(string reason)
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOperationException($"Only a placed order can be declined. This one is {Status}.");
        }

        Status = OrderStatus.Declined;
        FailureReason = reason;
        Version = Guid.NewGuid();
    }

    /// <summary>"Only a paid order can be refunded."</summary>
    public Result Refund()
    {
        if (Status != OrderStatus.Paid)
        {
            return Result.Invalid(new ValidationError($"Only a paid order can be refunded. This one is {Status}."));
        }

        Status = OrderStatus.Refunded;
        Version = Guid.NewGuid();
        return Result.Success();
    }
}
