using Ardalis.Result;
using FluentValidation;
using Mediator;
using TicketTide.Tickets.Contracts;
using TicketTide.Tickets.Internal.Data;

namespace TicketTide.Tickets.Internal.Integrations;

public sealed class IssueTicketsHandler(TicketsDbContext db) : ICommandHandler<IssueTickets, Result<IReadOnlyList<TicketDetails>>>
{
    public async ValueTask<Result<IReadOnlyList<TicketDetails>>> Handle(IssueTickets command, CancellationToken cancellationToken)
    {
        var tickets = Enumerable.Range(1, command.Quantity)
            .Select(number => new Ticket { OrderId = command.OrderId, Number = number, HolderEmail = command.HolderEmail })
            .ToList();
        db.Tickets.AddRange(tickets);
        await db.SaveChangesAsync(cancellationToken);

        if (command.SimulateFailure)
        {
            // The tickets are already written (inside the caller's transaction). Crashing now shows that the
            // rollback removes them, along with everything the other modules did before this step.
            throw new InvalidOperationException("Ticketing crashed after writing the tickets (simulated).");
        }

        return tickets.Select(t => t.ToDetails()).ToList();
    }
}

internal sealed class IssueTicketsValidator : AbstractValidator<IssueTickets>
{
    public IssueTicketsValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.HolderEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
