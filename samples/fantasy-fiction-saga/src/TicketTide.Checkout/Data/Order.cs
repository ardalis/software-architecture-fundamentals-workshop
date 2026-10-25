namespace TicketTide.Checkout.Data;

internal enum OrderStatus
{
    /// <summary>The three requests are out. The fan is waiting for the answer.</summary>
    Pending,
    Confirmed,

    /// <summary>A step failed. The orchestrator is undoing every step that succeeded, and still waiting for replies.</summary>
    Compensating,
    Failed,
}

/// <summary>The three steps the orchestrator sends at once.</summary>
internal enum SagaStep
{
    HoldSeats,
    ChargeCard,
    IssueTickets,
}

/// <summary>Where one step is. The order is only finished when no step is waiting for a reply.</summary>
internal enum StepState
{
    /// <summary>The request was never sent (the order was rejected before the saga started).</summary>
    NotSent,

    /// <summary>The request is in flight. Its reply has not arrived yet.</summary>
    Requested,
    Succeeded,

    /// <summary>The module said no. Nothing to undo.</summary>
    Failed,

    /// <summary>The step succeeded, but the order failed, so its compensation is in flight.</summary>
    Compensating,

    /// <summary>The module confirmed the step is undone.</summary>
    Compensated,
}

/// <summary>
/// The saga state. Replies arrive in any order, so the orchestrator tracks each step separately.
/// <see cref="History"/> records what happened so you can follow the saga from <c>GET /orders/{id}</c>.
/// </summary>
internal sealed class Order
{
    public static readonly SagaStep[] Steps = [SagaStep.HoldSeats, SagaStep.ChargeCard, SagaStep.IssueTickets];

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string EventId { get; init; }

    public string? EventName { get; set; }

    public int Quantity { get; init; }

    public required string FanEmail { get; init; }

    public required string PaymentToken { get; init; }

    public int SimulateTicketingFailures { get; init; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public decimal? Amount { get; set; }

    public StepState HoldSeats { get; private set; }

    public StepState ChargeCard { get; private set; }

    public StepState IssueTickets { get; private set; }

    public List<string> TicketCodes { get; set; } = [];

    public string? FailureReason { get; private set; }

    public List<string> History { get; set; } = ["Order placed."];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Optimistic concurrency token (PostgreSQL's xmin), so two replies can't overwrite each other.</summary>
    public uint Version { get; set; }

    public bool IsFinished => Status is OrderStatus.Confirmed or OrderStatus.Failed;

    public StepState StateOf(SagaStep step) => step switch
    {
        SagaStep.HoldSeats => HoldSeats,
        SagaStep.ChargeCard => ChargeCard,
        SagaStep.IssueTickets => IssueTickets,
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    public void MoveStep(SagaStep step, StepState state)
    {
        switch (step)
        {
            case SagaStep.HoldSeats:
                HoldSeats = state;
                break;
            case SagaStep.ChargeCard:
                ChargeCard = state;
                break;
            case SagaStep.IssueTickets:
                IssueTickets = state;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(step));
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<SagaStep> StepsThatAre(StepState state) => [.. Steps.Where(s => StateOf(s) == state)];

    /// <summary>
    /// True when every original reply is in and every compensation has been confirmed.
    /// Until then, an atomic saga cannot give the fan an answer.
    /// </summary>
    public bool NothingInFlight => Steps.All(s => StateOf(s) is not (StepState.Requested or StepState.Compensating));

    public void Note(string note)
    {
        History.Add(note);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Confirm()
    {
        Status = OrderStatus.Confirmed;
        Note("All three steps are done. Tickets confirmed.");
    }

    /// <summary>The order failed before any request was sent, so there is nothing to undo.</summary>
    public void Reject(string reason)
    {
        FailureReason = reason;
        Status = OrderStatus.Failed;
        Note("Purchase failed.");
    }

    public void StartCompensating(string reason)
    {
        FailureReason = reason;
        Status = OrderStatus.Compensating;
        Note("Compensating: undoing every step that succeeded, and waiting for the replies still in flight.");
    }

    /// <summary>Ends a compensating order once nothing is in flight. Returns true if the order is now failed.</summary>
    public bool TryFinishCompensating()
    {
        if (Status != OrderStatus.Compensating || !NothingInFlight)
        {
            return false;
        }

        TicketCodes = []; // Any tickets were voided.
        Status = OrderStatus.Failed;
        Note("Every step is undone. Purchase failed.");
        return true;
    }
}
