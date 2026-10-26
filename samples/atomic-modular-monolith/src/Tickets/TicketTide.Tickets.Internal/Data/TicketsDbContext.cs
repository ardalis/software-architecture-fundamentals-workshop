using Microsoft.EntityFrameworkCore;
using TicketTide.SharedKernel.Data;
using TicketTide.Tickets.Contracts;

namespace TicketTide.Tickets.Internal.Data;

// Public only so the Mediator handlers (which must be public) can depend on it. Other modules can't see it: they
// reference TicketTide.Tickets.Contracts, never this project.
public sealed class TicketsDbContext(DbContextOptions<TicketsDbContext> options, UnitOfWork unitOfWork)
    : ModuleDbContext(options, unitOfWork)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The module's own schema in the shared database: tickets.Tickets. No foreign key to the orders table:
        // that belongs to another module, which is free to change it.
        modelBuilder.HasDefaultSchema("tickets");

        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.HolderEmail).HasMaxLength(254);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.OrderId);
        });
    }
}

internal static class TicketMapping
{
    public static TicketDetails ToDetails(this Ticket ticket) =>
        new(ticket.Id, ticket.Code, ticket.HolderEmail, ticket.Status.ToString());
}
