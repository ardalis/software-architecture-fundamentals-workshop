using Microsoft.EntityFrameworkCore;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Inventory.Data;

internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
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
            e.Property(x => x.Version).IsRowVersion();
        });
    }
}

internal sealed class Event
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Venue { get; init; }

    public DateTimeOffset StartsAt { get; init; }

    public decimal Price { get; init; }

    public int Capacity { get; init; }

    public int SeatsHeld { get; set; }
}

internal enum SeatHoldStatus
{
    Held,
    Released,
}

/// <summary>
/// A seat hold, plus what Inventory has heard about the order since. The failure events race each other,
/// so Inventory writes down each one as it arrives. The order they arrive in does not matter.
/// </summary>
internal sealed class SeatHold
{
    public Guid OrderId { get; init; }

    public required string EventId { get; init; }

    public int Quantity { get; init; }

    public decimal Amount { get; init; }

    public SeatHoldStatus Status { get; set; }

    /// <summary>Why the seats were released. Inventory passes it on to Checkout in Seats Released.</summary>
    public string? ReleaseReason { get; set; }

    /// <summary>
    /// The failure events Inventory has heard about this order, in the order they arrived.
    /// Saved in inventorydb, so the next event knows what came before it.
    /// </summary>
    public List<string> Heard { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL's xmin), so two events can't overwrite each other.</summary>
    public uint Version { get; set; }

    /// <summary>
    /// Inventory is the last service upstream, so it may only report the release once every compensation
    /// downstream is done: the card was declined (nothing to refund), or Ticketing failed and the card was refunded.
    /// </summary>
    public bool EveryCompensationIsIn =>
        Heard.Contains(nameof(PaymentDeclined))
        || (Heard.Contains(nameof(TicketIssueFailed)) && Heard.Contains(nameof(CardRefunded)));
}
