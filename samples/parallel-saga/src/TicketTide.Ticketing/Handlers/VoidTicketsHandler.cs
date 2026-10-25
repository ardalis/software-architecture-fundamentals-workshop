using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketTide.Messaging;
using TicketTide.Ticketing.Contracts;
using TicketTide.Ticketing.Data;

namespace TicketTide.Ticketing.Handlers;

internal sealed class VoidTicketsHandler(TicketingDbContext db, IMessageBus bus, ILogger<VoidTicketsHandler> logger)
    : IMessageHandler<VoidTickets>
{
    public async Task HandleAsync(VoidTickets message, MessageContext context, CancellationToken cancellationToken)
    {
        var issue = await db.TicketIssues.FirstOrDefaultAsync(t => t.OrderId == message.OrderId, cancellationToken);
        if (issue is null)
        {
            // The void arrived before the issue request (messages can overtake each other).
            // Record it, so a late IssueTickets for this order issues nothing.
            db.TicketIssues.Add(new TicketIssue
            {
                OrderId = message.OrderId,
                EventId = string.Empty,
                Status = TicketIssueStatus.Voided,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (issue.Status != TicketIssueStatus.Voided)
        {
            issue.Status = TicketIssueStatus.Voided;
            issue.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Voided {Count} tickets for order {OrderId}", issue.TicketCodes.Count, message.OrderId);
        }

        await bus.PublishAsync(new TicketsVoided(message.OrderId), cancellationToken);
    }
}
