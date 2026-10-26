using Microsoft.EntityFrameworkCore;
using TicketTide.SharedKernel.Data;

namespace TicketTide.Orders.Internal.Data;

// Public only so the Mediator handlers (which must be public) can depend on it. Other modules can't see it.
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options, UnitOfWork unitOfWork)
    : ModuleDbContext(options, unitOfWork)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The module's own schema in the shared database: orders.Orders. No foreign keys to events or tickets:
        // those tables belong to other modules.
        modelBuilder.HasDefaultSchema("orders");

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.EventId).HasMaxLength(100);
            e.Property(x => x.EventName).HasMaxLength(200);
            e.Property(x => x.FanEmail).HasMaxLength(254);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentReference).HasMaxLength(100);
            e.Property(x => x.Version).IsConcurrencyToken();
        });
    }
}
