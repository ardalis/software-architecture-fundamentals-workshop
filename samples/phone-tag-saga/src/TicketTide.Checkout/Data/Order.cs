namespace TicketTide.Checkout.Data;

/// <summary>
/// With no orchestrator, Checkout cannot see the steps in the middle of the chain.
/// It only knows that the order was placed and, when the reply comes back, how it ended.
/// </summary>
internal enum OrderStatus
{
    Placed,
    Confirmed,
    Failed,
    NeedsAttention,
}

/// <summary>
/// The order, as Checkout sees it. <see cref="History"/> records the steps every module reported back
/// up the chain, so you can follow the saga from <c>GET /orders/{id}</c>.
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

    // A module in the chain could not undo its step. A person has to look at this order.
    public void FailNeedingAttention(string reason)
    {
        FailureReason = reason;
        MoveTo(OrderStatus.NeedsAttention, "Purchase failed, and not every step could be undone. Needs manual attention.");
    }
}
