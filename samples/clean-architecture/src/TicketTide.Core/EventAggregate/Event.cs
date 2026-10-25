using Ardalis.Result;

namespace TicketTide.Core.EventAggregate;

/// <summary>An event on sale at a venue, and how many of its seats are sold.</summary>
public sealed class Event
{
    // For EF Core.
    private Event()
    {
    }

    public Event(string id, string name, string venue, DateTimeOffset startsAt, decimal price, int capacity)
    {
        Id = id;
        Name = name;
        Venue = venue;
        StartsAt = startsAt;
        Price = price;
        Capacity = capacity;
    }

    public string Id { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Venue { get; private set; } = string.Empty;

    public DateTimeOffset StartsAt { get; private set; }

    public decimal Price { get; private set; }

    public int Capacity { get; private set; }

    public int SeatsSold { get; private set; }

    public int SeatsAvailable => Capacity - SeatsSold;

    /// <summary>Optimistic concurrency token, so two fans can't both buy the last seats.</summary>
    public uint Version { get; private set; }

    public Result EnsureSeatsAvailable(int quantity) =>
        quantity <= SeatsAvailable
            ? Result.Success()
            : Result.Invalid(new ValidationError($"Not enough seats: {Name} has {SeatsAvailable} left."));

    public Result SellSeats(int quantity)
    {
        var available = EnsureSeatsAvailable(quantity);
        if (!available.IsSuccess)
        {
            return available;
        }

        SeatsSold += quantity;
        return Result.Success();
    }

    public void ReturnSeats(int quantity)
    {
        if (quantity > SeatsSold)
        {
            throw new InvalidOperationException($"Can't return {quantity} seats: only {SeatsSold} are sold.");
        }

        SeatsSold -= quantity;
    }
}
