using Mediator;
using Microsoft.Extensions.Logging;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Payments.Handoffs;

internal static class TicketingSender
{
    // One attempt to hand off to Ticketing. An exception counts as a failed attempt, so the caller retries it
    // like any other technical failure. The details are logged, not shown to the fan.
    public static async Task<IssueTicketsResult> TryIssueTicketsAsync(
        this ISender sender, IssueTickets command, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.Send(command, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ticketing failed for order {OrderId}", command.OrderId);
            return IssueTicketsResult.Failed("Ticketing is unavailable.");
        }
    }
}
