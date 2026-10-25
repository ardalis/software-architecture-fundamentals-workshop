using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TicketTide.Payments.Data;

// Public only so the Mediator handlers (which must be public) can depend on it.
// Other modules cannot see it: they reference TicketTide.Payments.Contracts, never this project.
public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>();
        });
    }
}

public enum PaymentStatus
{
    Charged,
    Declined,
    Refunded,
}

public sealed class Payment
{
    public Guid OrderId { get; init; }

    public decimal Amount { get; init; }

    public PaymentStatus Status { get; set; }

    /// <summary>The provider's id for the charge (and later the refund).</summary>
    public string? ProviderReference { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class PaymentsDatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
