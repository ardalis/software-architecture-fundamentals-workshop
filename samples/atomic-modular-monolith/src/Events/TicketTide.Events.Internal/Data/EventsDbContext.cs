using Microsoft.EntityFrameworkCore;
using TicketTide.SharedKernel.Data;

namespace TicketTide.Events.Internal.Data;

// Public only so the Mediator handlers (which must be public) can depend on it. Other modules can't see it: they
// reference TicketTide.Events.Contracts, never this project.
public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options, UnitOfWork unitOfWork)
    : ModuleDbContext(options, unitOfWork)
{
    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The module's own schema in the shared database: events.Events.
        modelBuilder.HasDefaultSchema("events");

        modelBuilder.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Ignore(x => x.SeatsAvailable);
        });
    }
}
