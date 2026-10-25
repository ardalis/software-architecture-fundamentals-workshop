using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TicketTide.Inventory.Data;

// Creates the schema and seeds the events before the API starts taking requests.
internal sealed class InventoryDatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Events.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Events.AddRange(SeedEvents.All);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class SeedEvents
{
    public static Event[] All =>
    [
        new()
        {
            Id = "midnight-tide",
            Name = "The Midnight Tide",
            Venue = "Harbor Club",
            StartsAt = new DateTimeOffset(2026, 11, 14, 20, 0, 0, TimeSpan.Zero),
            Price = 45.00m,
            Capacity = 500,
        },
        new()
        {
            Id = "laugh-track-live",
            Name = "Laugh Track Live",
            Venue = "The Comedy Cellar",
            StartsAt = new DateTimeOffset(2026, 11, 21, 19, 30, 0, TimeSpan.Zero),
            Price = 25.00m,
            Capacity = 120,
        },
        new()
        {
            Id = "back-room-sessions",
            Name = "Back Room Sessions",
            Venue = "The Back Room",
            StartsAt = new DateTimeOffset(2026, 12, 5, 21, 0, 0, TimeSpan.Zero),
            Price = 30.00m,
            Capacity = 4,
        },
    ];
}
