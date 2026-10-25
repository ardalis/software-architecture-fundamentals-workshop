using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Data;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// The orchestrator's retry loop. It polls checkoutdb for pending orders whose next Issue Tickets attempt is due,
/// and hands each one back to <see cref="CheckoutOrchestrator"/>. Because the pending state lives in the database,
/// retries carry on after the API restarts.
/// </summary>
/// <remarks>
/// Kept small for teaching. A production retrier would add jitter and a growing backoff, give up and alert someone
/// after a limit, and claim rows (for example with <c>FOR UPDATE SKIP LOCKED</c>) when more than one instance runs.
/// </remarks>
internal sealed class TicketingRetryService(IServiceScopeFactory scopeFactory, ILogger<TicketingRetryService> logger)
    : BackgroundService
{
    private const int BatchSize = 20;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

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
                // Keep the loop alive: a database blip now should not stop every future retry.
                logger.LogError(ex, "Retrying pending orders failed");
            }
        }
    }

    private async Task RetryDueOrdersAsync(CancellationToken stoppingToken)
    {
        List<Guid> dueOrderIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var now = DateTimeOffset.UtcNow;
            dueOrderIds = await scope.ServiceProvider.GetRequiredService<CheckoutDbContext>().Orders
                .AsNoTracking()
                .Where(o => o.Status == OrderStatus.Pending && o.NextTicketingAttemptAt <= now)
                .OrderBy(o => o.NextTicketingAttemptAt)
                .Select(o => o.Id)
                .Take(BatchSize)
                .ToListAsync(stoppingToken);
        }

        // One scope per order, so each retry gets a fresh DbContext.
        foreach (var orderId in dueOrderIds)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CheckoutOrchestrator>().RetryIssueTicketsAsync(orderId);
        }
    }
}
