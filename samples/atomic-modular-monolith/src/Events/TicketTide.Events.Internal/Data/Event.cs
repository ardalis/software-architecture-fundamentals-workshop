namespace TicketTide.Events.Internal.Data;

/// <summary>An event on sale at a venue, and how many of its seats are sold.</summary>
public sealed class Event
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Venue { get; init; }

    public DateTimeOffset StartsAt { get; init; }

    public decimal Price { get; init; }

    public int Capacity { get; init; }

    public int SeatsSold { get; set; }

    public int SeatsAvailable => Capacity - SeatsSold;
}
