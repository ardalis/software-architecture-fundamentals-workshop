namespace TicketTide.Checkout.Data;

internal enum OrderStatus
{
    /// <summary>The order is in. The fan sees this until every module has replied.</summary>
    Pending,
    Confirmed,
    Failed,
}

/// <summary>
/// The saga state. Replies arrive in any order, so the orchestrator tracks each step separately.
/// <see cref="History"/> records what happened so you can follow the saga from <c>GET /orders/{id}</c>.
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

    public decimal? Amount { get; set; }

    public bool SeatsHeld { get; set; }

    public bool PaymentCharged { get; set; }

    public bool TicketsIssued { get; set; }

    /// <summary>When the orchestrator should send Issue Tickets again, after Ticketing failed.</summary>
    public DateTimeOffset? RetryTicketingAt { get; set; }

    public int TicketingRetries { get; set; }

    public List<string> TicketCodes { get; set; } = [];

    public string? FailureReason { get; private set; }

    public List<string> History { get; set; } = ["Order placed."];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Optimistic concurrency token (PostgreSQL's xmin), so two replies can't overwrite each other.</summary>
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
        Note("All three steps are done. Tickets emailed.");
    }

    public void Fail(string reason)
    {
        FailureReason = reason;
        RetryTicketingAt = null;
        Status = OrderStatus.Failed;
        Note("Purchase failed.");
    }
}
