using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;
using TicketTide.Ticketing.Data;

namespace TicketTide.Ticketing.Handlers;

/// <summary>
/// The card is charged: issue the tickets, then publish Tickets Issued for Checkout.
/// The saga is atomic, so there are no retries. One failure publishes Ticket Issue Failed, and Payments and
/// Inventory, which both subscribe to it, undo their steps.
/// </summary>
internal sealed class PaymentReceivedHandler(TicketingDbContext db, IMessageBus bus, ILogger<PaymentReceivedHandler> logger)
    : IMessageHandler<PaymentReceived>
{
    public async Task HandleAsync(PaymentReceived message, MessageContext context, CancellationToken cancellationToken)
    {
        var issue = await db.TicketIssues.FirstOrDefaultAsync(t => t.OrderId == message.OrderId, cancellationToken);
        if (issue is not null)
        {
            // A redelivery. Publish the same outcome again; never try a second time.
            await (issue.Status == TicketIssueStatus.Issued
                ? bus.PublishAsync(new TicketsIssued(message.OrderId, message.EventName, message.Amount, issue.TicketCodes), cancellationToken)
                : bus.PublishAsync(new TicketIssueFailed(message.OrderId, issue.LastError!), cancellationToken));
            return;
        }

        issue = new TicketIssue
        {
            OrderId = message.OrderId,
            EventId = message.EventId,
            Quantity = message.Quantity,
            Attempts = 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.TicketIssues.Add(issue);

        // Failure injection for the demo: any simulated failure fails the only attempt.
        if (message.SimulateTicketingFailures > 0)
        {
            issue.Status = TicketIssueStatus.Failed;
            issue.LastError = $"Ticketing is unavailable (simulated failure 1 of {message.SimulateTicketingFailures}).";
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning("Ticketing failed for order {OrderId}: {Error}", message.OrderId, issue.LastError);
            await bus.PublishAsync(new TicketIssueFailed(message.OrderId, issue.LastError), cancellationToken);
            return;
        }

        issue.Status = TicketIssueStatus.Issued;
        issue.TicketCodes = Enumerable.Range(1, message.Quantity)
            .Select(n => $"TT-{message.OrderId.ToString("N")[..8].ToUpperInvariant()}-{n:00}")
            .ToList();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Issued {Count} tickets for order {OrderId}", issue.TicketCodes.Count, message.OrderId);
        await bus.PublishAsync(new TicketsIssued(message.OrderId, message.EventName, message.Amount, issue.TicketCodes), cancellationToken);
    }
}
