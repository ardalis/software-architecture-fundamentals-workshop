using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TicketTide.Ticketing.Data;

internal sealed class TicketingDbContext(DbContextOptions<TicketingDbContext> options) : DbContext(options)
{
    public DbSet<TicketIssue> TicketIssues => Set<TicketIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TicketIssue>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.Status).HasConversion<string>();
        });
    }
}

internal enum TicketIssueStatus
{
    Failed,
    Issued,

    /// <summary>Cancelled by a compensating command. Voided tickets cannot be scanned.</summary>
    Voided,
}

/// <summary>Every attempt to issue tickets for an order, so retries and failures are visible.</summary>
internal sealed class TicketIssue
{
    public Guid OrderId { get; init; }

    public required string EventId { get; init; }

    public int Quantity { get; init; }

    public int Attempts { get; set; }

    public TicketIssueStatus Status { get; set; }

    public List<string> TicketCodes { get; set; } = [];

    public string? LastError { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class TicketingDatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TicketingDbContext>().Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
