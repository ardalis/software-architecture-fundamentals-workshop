using Microsoft.EntityFrameworkCore;

namespace TicketTide.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.Version).IsRowVersion(); // PostgreSQL's xmin
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.Version).IsRowVersion();
            e.HasMany(x => x.Tickets).WithOne().HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.Code).IsUnique();

            // Tickets change on their own rows (a refund, or a transfer), so they need their own concurrency token.
            e.Property<uint>("Version").IsRowVersion();
        });
    }
}
