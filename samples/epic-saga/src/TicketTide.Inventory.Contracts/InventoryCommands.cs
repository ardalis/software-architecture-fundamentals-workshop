using Mediator;

namespace TicketTide.Inventory.Contracts;

/// <summary>Hold seats for an order. Holding the same order twice returns the existing hold.</summary>
public sealed record HoldSeats(Guid OrderId, string EventId, int Quantity) : ICommand<HoldSeatsResult>;

public sealed record HoldSeatsResult(bool Succeeded, string EventName, decimal Amount, string? FailureReason)
{
    public static HoldSeatsResult Held(string eventName, decimal amount) => new(true, eventName, amount, null);

    public static HoldSeatsResult Rejected(string reason) => new(false, string.Empty, 0m, reason);
}

/// <summary>Compensating command: give held seats back. Safe to call more than once.</summary>
public sealed record ReleaseSeats(Guid OrderId) : ICommand;
