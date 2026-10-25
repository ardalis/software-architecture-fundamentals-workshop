using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Ticketing.Contracts;
using TicketTide.Ticketing.Data;

namespace TicketTide.Ticketing.Handlers;

public sealed class IssueTicketsHandler(TicketingDbContext db, ILogger<IssueTicketsHandler> logger)
    : ICommandHandler<IssueTickets, IssueTicketsResult>
{
    public async ValueTask<IssueTicketsResult> Handle(IssueTickets command, CancellationToken cancellationToken)
    {
        var issue = await db.TicketIssues.FirstOrDefaultAsync(t => t.OrderId == command.OrderId, cancellationToken);
        if (issue is { Status: TicketIssueStatus.Issued })
        {
            return IssueTicketsResult.Issued(issue.TicketCodes); // Already done: issuing is idempotent.
        }

        if (issue is null)
        {
            issue = new TicketIssue { OrderId = command.OrderId, EventId = command.EventId, Quantity = command.Quantity };
            db.TicketIssues.Add(issue);
        }

        issue.Attempts++;
        issue.UpdatedAt = DateTimeOffset.UtcNow;

        // Failure injection for the demo: fail the first N attempts for this order.
        if (issue.Attempts <= command.SimulatedFailures)
        {
            issue.Status = TicketIssueStatus.Failed;
            issue.LastError = $"Ticketing is unavailable (simulated failure {issue.Attempts} of {command.SimulatedFailures}).";
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning("Ticketing failed for order {OrderId}: {Error}", command.OrderId, issue.LastError);
            return IssueTicketsResult.Failed(issue.LastError);
        }

        issue.Status = TicketIssueStatus.Issued;
        issue.LastError = null;
        issue.TicketCodes = Enumerable.Range(1, command.Quantity)
            .Select(n => $"TT-{command.OrderId.ToString("N")[..8].ToUpperInvariant()}-{n:00}")
            .ToList();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Issued {Count} tickets for order {OrderId}", issue.TicketCodes.Count, command.OrderId);
        return IssueTicketsResult.Issued(issue.TicketCodes);
    }
}
