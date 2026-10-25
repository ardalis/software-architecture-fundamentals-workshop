using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketTide.Checkout.Contracts;
using TicketTide.Payments.Data;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Payments.Handoffs;

/// <summary>
/// Finishes the hand-offs to Ticketing that Payments could not finish while the fan was waiting.
/// Every second, it retries each pending hand-off. When Ticketing issues the tickets, the data catches up:
/// Payments tells Checkout (<see cref="TicketsIssued"/>), and Checkout confirms the order and emails the fan.
/// In choreography each service knows who to tell next. The fan's request is long gone, so there is no chain
/// of replies to travel back up; Payments tells Checkout directly.
/// </summary>
internal sealed class TicketHandoffRetrier(IServiceScopeFactory scopeFactory, ILogger<TicketHandoffRetrier> logger)
    : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RetryInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var orderId in await PendingHandoffsAsync(stoppingToken))
                {
                    await RetryAsync(orderId, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Keep going: the next tick tries again.
                logger.LogError(ex, "Retrying the pending Ticketing hand-offs failed");
            }
        }
    }

    private async Task<List<Guid>> PendingHandoffsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        return await db.TicketHandoffs.AsNoTracking()
            .Where(h => h.Status == TicketHandoffStatus.Pending)
            .OrderBy(h => h.CreatedAt)
            .Select(h => h.OrderId)
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    // Each retry gets its own scope, so one order's failure cannot leave state behind for the next.
    private async Task RetryAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var handoff = await db.TicketHandoffs.FirstAsync(h => h.OrderId == orderId, cancellationToken);
        handoff.Attempts++;
        handoff.UpdatedAt = DateTimeOffset.UtcNow;

        // Ticketing is idempotent: once it has issued the tickets, asking again returns the same codes.
        var tickets = await sender.TryIssueTicketsAsync(
            new IssueTickets(handoff.OrderId, handoff.EventId, handoff.Quantity, handoff.SimulateTicketingFailures),
            logger,
            cancellationToken);
        if (!tickets.Succeeded)
        {
            handoff.LastError = tickets.FailureReason;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var told = await sender.Send(new TicketsIssued(handoff.OrderId, tickets.TicketCodes), cancellationToken);
        if (told.Accepted)
        {
            handoff.Status = TicketHandoffStatus.Done;
            handoff.LastError = null;
            logger.LogInformation("Ticketing recovered for order {OrderId} after {Attempts} background attempt(s)",
                handoff.OrderId, handoff.Attempts);
        }
        else
        {
            handoff.LastError = "Checkout was not ready for the news yet.";
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
