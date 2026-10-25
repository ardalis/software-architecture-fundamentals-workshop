using Ardalis.Result;
using TicketTide.Core.EventAggregate;

namespace TicketTide.Core.OrderAggregate;

/// <summary>
/// A fan's order for tickets to one event. This is the aggregate root: the order's rules live here, and its
/// tickets change only through it.
/// </summary>
public sealed class Order
{
    private readonly List<Ticket> _tickets = [];

    // For EF Core.
    private Order()
    {
    }

    private Order(Event evt, int quantity, string fanEmail)
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

    public IReadOnlyList<Ticket> Tickets => _tickets.AsReadOnly();

    /// <summary>Optimistic concurrency token, so two requests can't both refund the same order.</summary>
    public uint Version { get; private set; }

    public static Order Place(Event evt, int quantity, string fanEmail) => new(evt, quantity, fanEmail);

    /// <summary>The card was charged, so issue one ticket per seat, all held by the fan who bought them.</summary>
    public void MarkPaid(string paymentReference)
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOperationException($"Only a placed order can be paid. This one is {Status}.");
        }

        Status = OrderStatus.Paid;
        PaymentReference = paymentReference;
        for (var i = 0; i < Quantity; i++)
        {
            _tickets.Add(new Ticket(FanEmail));
        }
    }

    public void MarkDeclined(string reason)
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOperationException($"Only a placed order can be declined. This one is {Status}.");
        }

        Status = OrderStatus.Declined;
        FailureReason = reason;
    }

    /// <summary>Support refunds a paid order, and every ticket in it stops working.</summary>
    public Result Refund()
    {
        if (Status != OrderStatus.Paid)
        {
            return Result.Invalid(new ValidationError($"Only a paid order can be refunded. This one is {Status}."));
        }

        Status = OrderStatus.Refunded;
        foreach (var ticket in _tickets)
        {
            ticket.Refund();
        }

        return Result.Success();
    }
}
