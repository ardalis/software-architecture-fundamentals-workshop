using Ardalis.Result;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.Events.Contracts;
using TicketTide.Events.Internal.Data;

namespace TicketTide.Events.Internal.Integrations;

public sealed class ReturnSeatsHandler(EventsDbContext db) : ICommandHandler<ReturnSeats, Result>
{
    public async ValueTask<Result> Handle(ReturnSeats command, CancellationToken cancellationToken)
    {
        var updated = await db.Events
            .Where(e => e.Id == command.EventId && e.SeatsSold >= command.Quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsSold, e => e.SeatsSold - command.Quantity), cancellationToken);

        return updated == 1
            ? Result.Success()
            : Result.Error($"Can't return {command.Quantity} seats to {command.EventId}: that many aren't sold.");
    }
}

internal sealed class ReturnSeatsValidator : AbstractValidator<ReturnSeats>
{
    public ReturnSeatsValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
