using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TicketTide.Payments.Data;

// Public only so the Mediator handlers (which must be public) can depend on it.
// Other modules cannot see it: they reference TicketTide.Payments.Contracts, never this project.
public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<TicketHandoff> TicketHandoffs => Set<TicketHandoff>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<TicketHandoff>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.Status);
        });
    }
}

// There is no Refunded status: in this saga nothing is rolled back once the card is charged.
public enum PaymentStatus
{
    Charged,
    Declined,
}

public sealed class Payment
{
    public Guid OrderId { get; init; }

    public decimal Amount { get; init; }

    public PaymentStatus Status { get; set; }

    /// <summary>The provider's id for the charge.</summary>
    public string? ProviderReference { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public enum TicketHandoffStatus
{
    /// <summary>Ticketing has not issued the tickets yet. The background retrier keeps trying.</summary>
    Pending,

    /// <summary>Ticketing issued the tickets and Checkout has been told.</summary>
    Done,
}

/// <summary>
/// A hand-off to Ticketing that Payments could not finish while the fan was waiting.
/// Payments owns the hand-off, so it records it in its own database and keeps retrying.
/// </summary>
public sealed class TicketHandoff
{
    public Guid OrderId { get; init; }

    public required string EventId { get; init; }

    public int Quantity { get; init; }

    public int SimulateTicketingFailures { get; init; }

    public TicketHandoffStatus Status { get; set; }

    /// <summary>Background attempts so far. The attempts made while the fan was waiting are not counted here.</summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
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
