namespace TicketTide.Checkout.Data;

internal enum OrderStatus
{
    Placed,
    SeatsHeld,
    PaymentCharged,

    /// <summary>Seats held and card charged, but Ticketing failed. The orchestrator keeps retrying.</summary>
    Pending,
    Confirmed,
    Failed,
}

/// <summary>
/// The saga state. The orchestrator saves it after every step, and <see cref="History"/>
/// records what happened so you can follow the saga from <c>GET /orders/{id}</c>.
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

    public OrderStatus Status { get; private set; } = OrderStatus.Placed;

    public decimal? Amount { get; set; }

    public List<string> TicketCodes { get; set; } = [];

    public string? FailureReason { get; private set; }

    /// <summary>How many times the orchestrator has retried Issue Tickets after the first attempt failed.</summary>
    public int TicketingRetries { get; private set; }

    /// <summary>When to retry Issue Tickets. Set only while the order is <see cref="OrderStatus.Pending"/>.</summary>
    public DateTimeOffset? NextTicketingAttemptAt { get; private set; }

    public List<string> History { get; set; } = ["Order placed."];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void MoveTo(OrderStatus status, string note)
    {
        Status = status;
        History.Add(note);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Note(string note)
    {
        History.Add(note);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string reason)
    {
        FailureReason = reason;
        MoveTo(OrderStatus.Failed, "Purchase failed.");
    }

    /// <summary>Ticketing failed: the order stays in, and Issue Tickets is retried after <paramref name="delay"/>.</summary>
    public void WaitForTickets(string note, TimeSpan delay)
    {
        NextTicketingAttemptAt = DateTimeOffset.UtcNow + delay;
        MoveTo(OrderStatus.Pending, note);
    }

    /// <summary>Counts the retry that is about to run and returns its number (1 for the first retry).</summary>
    public int StartTicketingRetry() => ++TicketingRetries;

    public void Confirm(IReadOnlyList<string> ticketCodes, string note)
    {
        TicketCodes = [.. ticketCodes];
        NextTicketingAttemptAt = null;
        MoveTo(OrderStatus.Confirmed, note);
    }
}
