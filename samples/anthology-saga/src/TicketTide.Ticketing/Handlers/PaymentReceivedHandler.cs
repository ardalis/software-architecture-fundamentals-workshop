using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;
using TicketTide.Ticketing.Data;

namespace TicketTide.Ticketing.Handlers;

/// <summary>
/// When the payment is received, issue the tickets and publish <see cref="TicketsIssued"/>.
/// A failure here is technical, not a business "no", so nothing upstream is undone. The handler records the
/// failed attempt and throws. The message stays in the ticketing queue, and RabbitMQ redelivers it until
/// Ticketing recovers. That retry is what makes this saga eventually consistent.
/// </summary>
internal sealed class PaymentReceivedHandler(TicketingDbContext db, IMessageBus bus, ILogger<PaymentReceivedHandler> logger)
    : IMessageHandler<PaymentReceived>
{
    public async Task HandleAsync(PaymentReceived message, MessageContext context, CancellationToken cancellationToken)
    {
        var issue = await db.TicketIssues.FirstOrDefaultAsync(t => t.OrderId == message.OrderId, cancellationToken);
        if (issue?.Status == TicketIssueStatus.Issued)
        {
            await bus.PublishAsync(new TicketsIssued(message.OrderId, issue.TicketCodes), cancellationToken); // Idempotent.
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
        if (issue.Attempts <= message.SimulateTicketingFailures)
        {
            issue.Status = TicketIssueStatus.Failed;
            issue.LastError = $"Ticketing is unavailable (simulated failure {issue.Attempts} of {message.SimulateTicketingFailures}).";
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning("Ticketing failed for order {OrderId} on delivery {DeliveryAttempt}: {Error}",
                message.OrderId, context.DeliveryAttempt, issue.LastError);

            // Throwing means "not done": the message goes back on the queue and is redelivered.
            throw new InvalidOperationException(issue.LastError);
        }

        issue.Status = TicketIssueStatus.Issued;
        issue.LastError = null;
        issue.TicketCodes = Enumerable.Range(1, message.Quantity)
            .Select(n => $"TT-{message.OrderId.ToString("N")[..8].ToUpperInvariant()}-{n:00}")
            .ToList();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Issued {Count} tickets for order {OrderId} on delivery {DeliveryAttempt}",
            issue.TicketCodes.Count, message.OrderId, context.DeliveryAttempt);
        await bus.PublishAsync(new TicketsIssued(message.OrderId, issue.TicketCodes), cancellationToken);
    }
}
