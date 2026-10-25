using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Messaging;
using TicketTide.Ticketing.Contracts;
using TicketTide.Ticketing.Data;

namespace TicketTide.Ticketing.Handlers;

internal sealed class IssueTicketsHandler(TicketingDbContext db, IMessageBus bus, ILogger<IssueTicketsHandler> logger)
    : IMessageHandler<IssueTickets>
{
    public async Task HandleAsync(IssueTickets message, MessageContext context, CancellationToken cancellationToken)
    {
        var issue = await db.TicketIssues.FirstOrDefaultAsync(t => t.OrderId == message.OrderId, cancellationToken);
        switch (issue?.Status)
        {
            case TicketIssueStatus.Issued:
                await bus.PublishAsync(new TicketsIssued(message.OrderId, issue.TicketCodes), cancellationToken); // Idempotent.
                return;
            case TicketIssueStatus.Voided:
                await bus.PublishAsync(new TicketingFailed(message.OrderId, "The order was cancelled."), cancellationToken);
                return;
        }

        if (issue is null)
        {
            issue = new TicketIssue { OrderId = message.OrderId, EventId = message.EventId, Quantity = message.Quantity };
            db.TicketIssues.Add(issue);
        }

        issue.Attempts++;
        issue.UpdatedAt = DateTimeOffset.UtcNow;

        // Failure injection for the demo: fail the first N attempts for this order.
        if (issue.Attempts <= message.SimulatedFailures)
        {
            issue.Status = TicketIssueStatus.Failed;
            issue.LastError = $"Ticketing is unavailable (simulated failure {issue.Attempts} of {message.SimulatedFailures}).";
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning("Ticketing failed for order {OrderId}: {Error}", message.OrderId, issue.LastError);
            await bus.PublishAsync(new TicketingFailed(message.OrderId, issue.LastError), cancellationToken);
            return;
        }

        issue.Status = TicketIssueStatus.Issued;
        issue.LastError = null;
        issue.TicketCodes = Enumerable.Range(1, message.Quantity)
            .Select(n => $"TT-{message.OrderId.ToString("N")[..8].ToUpperInvariant()}-{n:00}")
            .ToList();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Issued {Count} tickets for order {OrderId}", issue.TicketCodes.Count, message.OrderId);
        await bus.PublishAsync(new TicketsIssued(message.OrderId, issue.TicketCodes), cancellationToken);
    }
}
