namespace TicketTide.Checkout.Data;

internal enum OrderStatus
{
    /// <summary>Order Placed is published. The other modules are passing events along the chain.</summary>
    Pending,
    Confirmed,

    /// <summary>A step failed. Checkout is waiting for every compensation before it tells the fan.</summary>
    Compensating,
    Failed,
}

/// <summary>
/// The order, as Checkout sees it. There is no orchestrator, so no one holds the saga's state:
/// Checkout records each event it hears in <see cref="History"/>, so you can follow the saga from
/// <c>GET /orders/{id}</c>.
/// </summary>
internal sealed class Order
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string EventId { get; init; }

    public string? EventName { get; set; }

    public int Quantity { get; init; }

    public required string FanEmail { get; init; }

    public required string PaymentToken { get; init; }

    public int SimulateTicketingFailures { get; init; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    /// <summary>Inventory works out the amount and announces it in Seats Held.</summary>
    public decimal? Amount { get; set; }

    public List<string> TicketCodes { get; set; } = [];

    public string? FailureReason { get; private set; }

    public List<string> History { get; set; } = ["Order placed. Checkout published Order Placed."];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Optimistic concurrency token (PostgreSQL's xmin), so two events can't overwrite each other.</summary>
    public uint Version { get; set; }

    public bool IsFinished => Status is OrderStatus.Confirmed or OrderStatus.Failed;

    /// <summary>Adds a note, unless a redelivered event already added it.</summary>
    public void Note(string note)
    {
        if (History.Contains(note))
        {
            return;
        }

        History.Add(note);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Confirm()
    {
        Status = OrderStatus.Confirmed;
        Note("Tickets confirmed. Tickets emailed.");
    }

    /// <summary>The first failure event Checkout hears. The order can no longer succeed, but it is not over yet.</summary>
    public void StartCompensating(string note)
    {
        if (Status == OrderStatus.Pending)
        {
            Status = OrderStatus.Compensating;
        }

        Note(note);
    }

    public void Fail(string reason, string note)
    {
        FailureReason = reason;
        Status = OrderStatus.Failed;
        Note(note);
    }
}
