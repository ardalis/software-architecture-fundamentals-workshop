namespace TicketTide.Checkout.Data;

public enum OrderStatus
{
    Placed,

    /// <summary>The seats are held and the card is charged, but the tickets are not issued yet.</summary>
    Pending,
    Confirmed,
    Failed,

    /// <summary>Something crashed, so Checkout cannot tell how far the chain got. A person has to check it.</summary>
    NeedsAttention,
}

// Public only so the TicketsIssued handler (Mediator handlers must be public) can depend on it.
// Other modules cannot see it: they reference TicketTide.Checkout.Contracts, never this project.

/// <summary>
/// Checkout's view of the purchase. There is no orchestrator, so Checkout only sees the replies that travel
/// back up the chain. <see cref="History"/> records the steps each module reported, so you can follow the saga
/// from <c>GET /orders/{id}</c>.
/// </summary>
public sealed class Order
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
}
