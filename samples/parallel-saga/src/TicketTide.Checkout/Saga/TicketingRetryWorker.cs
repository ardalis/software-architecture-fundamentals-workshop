using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Data;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// Sends Issue Tickets again for orders whose retry is due. The retry time is saved on the order,
/// so retries survive a restart of the API.
/// </summary>
internal sealed class TicketingRetryWorker(IServiceScopeFactory scopeFactory, ILogger<TicketingRetryWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RetryDueOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ticketing retry pass failed; trying again shortly");
            }
        }
    }

    private async Task RetryDueOrdersAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CheckoutDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<CheckoutOrchestrator>();

        var now = DateTimeOffset.UtcNow;
        var due = await db.Orders
            .Where(o => o.Status == OrderStatus.Pending && o.RetryTicketingAt != null && o.RetryTicketingAt <= now)
            .OrderBy(o => o.RetryTicketingAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var order in due)
        {
            try
            {
                await orchestrator.RetryTicketingAsync(order, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A reply changed this order at the same moment. The next pass will see the latest state.
                db.Entry(order).State = EntityState.Detached;
            }
        }
    }
}
