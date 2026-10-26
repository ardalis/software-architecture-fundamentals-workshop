using Ardalis.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.Tickets.Contracts;
using TicketTide.Tickets.Internal.Data;

namespace TicketTide.Tickets.Internal.Integrations;

public sealed class RefundTicketsHandler(TicketsDbContext db) : ICommandHandler<RefundTickets, Result<IReadOnlyList<TicketDetails>>>
{
    public async ValueTask<Result<IReadOnlyList<TicketDetails>>> Handle(RefundTickets command, CancellationToken cancellationToken)
    {
        var tickets = await db.Tickets.Where(t => t.OrderId == command.OrderId).OrderBy(t => t.Number).ToListAsync(cancellationToken);
        foreach (var ticket in tickets)
        {
            ticket.Status = TicketStatus.Refunded;
        }

        await db.SaveChangesAsync(cancellationToken);
        return tickets.Select(t => t.ToDetails()).ToList();
    }
}
