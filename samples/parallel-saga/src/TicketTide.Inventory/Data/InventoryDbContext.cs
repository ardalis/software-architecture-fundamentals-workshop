using Microsoft.EntityFrameworkCore;

namespace TicketTide.Inventory.Data;

// Public only so the Mediator query handler (which must be public) can depend on it.
// Other modules cannot see it: they reference TicketTide.Inventory.Contracts, never this project.
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<SeatHold>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>();
        });
    }
}

public sealed class Event
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Venue { get; init; }

    public DateTimeOffset StartsAt { get; init; }

    public decimal Price { get; init; }

    public int Capacity { get; init; }

    public int SeatsHeld { get; set; }
}

public enum SeatHoldStatus
{
    Held,
    Released,
}

public sealed class SeatHold
{
    public Guid OrderId { get; init; }

    public required string EventId { get; init; }

    public int Quantity { get; init; }

    public decimal Amount { get; init; }

    public SeatHoldStatus Status { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
