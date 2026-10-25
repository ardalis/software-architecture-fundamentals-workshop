using Ardalis.Result;
using Microsoft.EntityFrameworkCore;
using TicketTide.Core.EventAggregate;
using TicketTide.Core.Interfaces;
using TicketTide.Core.OrderAggregate;

namespace TicketTide.Infrastructure.Data;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Event> Events => Set<Event>();

    public DbSet<Order> Orders => Set<Order>();

    async Task<Result> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict();
        }
        catch (DbUpdateException ex)
        {
            return Result.Error(ex.InnerException?.Message ?? ex.Message);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.Version).IsRowVersion(); // PostgreSQL's xmin
            e.Ignore(x => x.SeatsAvailable);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.Version).IsRowVersion();

            // The order owns its tickets. EF Core fills the private list, so nothing outside the order can add one.
            e.HasMany(x => x.Tickets).WithOne().HasForeignKey("OrderId");
            e.Navigation(x => x.Tickets).UsePropertyAccessMode(PropertyAccessMode.Field);
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
