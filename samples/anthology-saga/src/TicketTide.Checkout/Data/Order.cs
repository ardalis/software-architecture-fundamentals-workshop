namespace TicketTide.Checkout.Data;

internal enum OrderStatus
{
    /// <summary>The order is in. The fan sees this until Ticketing has issued the tickets (or the saga fails).</summary>
    Pending,
    Confirmed,
    Failed,
}

/// <summary>
/// The order, as Checkout sees it. Checkout does not run the saga; it only hears the events the other modules
/// publish. <see cref="History"/> records each one, so you can follow the saga from <c>GET /orders/{id}</c>.
/// </summary>
internal sealed class Order
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string EventId { get; init; }

    /// <summary>Unknown until Inventory publishes Seats Held.</summary>
    public string? EventName { get; set; }

    public int Quantity { get; init; }

    public required string FanEmail { get; init; }

    public required string PaymentToken { get; init; }

    public int SimulateTicketingFailures { get; init; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    /// <summary>Inventory prices the order, so this is unknown until Seats Held arrives.</summary>
    public decimal? Amount { get; set; }

    public List<string> TicketCodes { get; set; } = [];

    public string? FailureReason { get; private set; }

    public List<string> History { get; set; } = ["Order received."];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Optimistic concurrency token (PostgreSQL's xmin), so two updates can't overwrite each other.</summary>
    public uint Version { get; set; }

    public bool IsFinished => Status != OrderStatus.Pending;

    public void Note(string note)
    {
        History.Add(note);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Confirm()
    {
        Status = OrderStatus.Confirmed;
        Note("Order confirmed. Tickets emailed.");
    }

    public void Fail(string reason)
    {
        FailureReason = reason;
        Status = OrderStatus.Failed;
        Note("Purchase failed.");
    }
}
