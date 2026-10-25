using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Core.EventAggregate;

namespace TicketTide.Infrastructure.Data;

// Creates the schema and seeds the events before the API starts taking requests.
internal sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
        new("midnight-tide", "The Midnight Tide", "Harbor Club", new DateTimeOffset(2026, 11, 14, 20, 0, 0, TimeSpan.Zero), 45.00m, 500),
        new("laugh-track-live", "Laugh Track Live", "The Comedy Cellar", new DateTimeOffset(2026, 11, 21, 19, 30, 0, TimeSpan.Zero), 25.00m, 120),
        new("back-room-sessions", "Back Room Sessions", "The Back Room", new DateTimeOffset(2026, 12, 5, 21, 0, 0, TimeSpan.Zero), 30.00m, 4),
    ];
}
