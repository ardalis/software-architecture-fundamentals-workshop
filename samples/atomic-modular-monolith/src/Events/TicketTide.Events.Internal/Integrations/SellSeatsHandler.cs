using Ardalis.Result;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TicketTide.Events.Contracts;
using TicketTide.Events.Internal.Data;

namespace TicketTide.Events.Internal.Integrations;

public sealed class SellSeatsHandler(EventsDbContext db) : ICommandHandler<SellSeats, Result>
{
    public async ValueTask<Result> Handle(SellSeats command, CancellationToken cancellationToken)
    {
        // "You can't buy more seats than are left", as one conditional UPDATE. Inside the caller's transaction it
        // also locks the event's row until the transaction ends, so two fans can't both buy the last seats: the
        // second one waits, then finds the seats gone.
        var updated = await db.Events
            .Where(e => e.Id == command.EventId && e.Capacity - e.SeatsSold >= command.Quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.SeatsSold, e => e.SeatsSold + command.Quantity), cancellationToken);
        if (updated == 1)
        {
            return Result.Success();
        }

        var evt = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken);
        return evt is null
            ? Result.NotFound($"There is no event with id {command.EventId}.")
            : Result.Invalid(new ValidationError($"Not enough seats: {evt.Name} has {evt.SeatsAvailable} left."));
    }
}

internal sealed class SellSeatsValidator : AbstractValidator<SellSeats>
{
    public SellSeatsValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
