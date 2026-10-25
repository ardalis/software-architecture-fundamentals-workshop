namespace TicketTide.Web.Data;

// Plain data classes. The rules about how they change live in the feature that changes them, under Features/.

public sealed class Event
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Venue { get; init; }

    public DateTimeOffset StartsAt { get; init; }

    public decimal Price { get; init; }

    public int Capacity { get; init; }

    public int SeatsSold { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL's xmin), so two fans can't both buy the last seats.</summary>
    public uint Version { get; set; }
}

public enum OrderStatus
{
    Paid,
    Declined,
    Refunded,
}

public sealed class Order
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string EventId { get; init; }

    public required string EventName { get; init; }

    public required string FanEmail { get; init; }

    public int Quantity { get; init; }

    public decimal Amount { get; init; }

    public OrderStatus Status { get; set; }

    /// <summary>The payment provider's reference for the charge, needed to refund it.</summary>
    public string? PaymentReference { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public List<Ticket> Tickets { get; init; } = [];

    /// <summary>Optimistic concurrency token, so two requests can't both refund the same order.</summary>
    public uint Version { get; set; }
}

public enum TicketStatus
{
    Paid,
    Refunded,
}

public sealed class Ticket
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid OrderId { get; init; }

    /// <summary>The code in the ticket's QR code. Door staff scan it at the entrance.</summary>
    public required string Code { get; set; }

    public required string HolderEmail { get; set; }

    public TicketStatus Status { get; set; }
}
