using Ardalis.Result;
using Microsoft.EntityFrameworkCore;
using TicketTide.Events.Contracts;
using TicketTide.Events.Internal;
using TicketTide.Events.Internal.Data;
using TicketTide.Events.Internal.Integrations;
using TicketTide.Events.Internal.UseCases.ListEvents;
using TicketTide.SharedKernel.Data;

namespace TicketTide.Events.Tests;

public sealed class EventsModuleTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteDatabase _database = new();
    private EventsDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        _db = _database.CreateContext<EventsDbContext>(new UnitOfWork(_database.Connection));
        await EventsDatabaseInitializer.SeedAsync(_db, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    [Fact]
    public void UsesOnlyOtherModulesContracts() => ModuleBoundaries.AssertUsesOnlyContracts(typeof(EventsModule).Assembly);

    [Fact]
    public async Task ListEvents_ShowsTheSeedEvents_InDateOrder()
    {
        var events = await new ListEventsHandler(_db).Handle(new ListEventsQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(["midnight-tide", "laugh-track-live", "back-room-sessions"], events.Select(e => e.Id));
        Assert.Equal(4, events.Single(e => e.Id == "back-room-sessions").SeatsAvailable);
    }

    [Fact]
    public async Task GetEventDetails_UnknownEvent_IsNotFound()
    {
        var result = await new GetEventDetailsHandler(_db).Handle(new GetEventDetails("no-such-event"), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SellSeats_TakesThemOffSale()
    {
        var result = await new SellSeatsHandler(_db).Handle(new SellSeats("back-room-sessions", 3), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await SeatsAvailableAsync("back-room-sessions"));
    }

    [Fact]
    public async Task SellSeats_MoreThanAreLeft_IsInvalid_AndSellsNothing()
    {
        var result = await new SellSeatsHandler(_db).Handle(new SellSeats("back-room-sessions", 5), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("Not enough seats: Back Room Sessions has 4 left.", Assert.Single(result.ValidationErrors).ErrorMessage);
        Assert.Equal(4, await SeatsAvailableAsync("back-room-sessions"));
    }

    [Fact]
    public async Task SellSeats_UnknownEvent_IsNotFound()
    {
        var result = await new SellSeatsHandler(_db).Handle(new SellSeats("no-such-event", 1), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ReturnSeats_PutsThemBackOnSale()
    {
        await new SellSeatsHandler(_db).Handle(new SellSeats("back-room-sessions", 4), TestContext.Current.CancellationToken);

        var result = await new ReturnSeatsHandler(_db).Handle(new ReturnSeats("back-room-sessions", 2), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await SeatsAvailableAsync("back-room-sessions"));
    }

    [Fact]
    public async Task ReturnSeats_MoreThanWereSold_IsAnError_AndChangesNothing()
    {
        var result = await new ReturnSeatsHandler(_db).Handle(new ReturnSeats("back-room-sessions", 1), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Equal(4, await SeatsAvailableAsync("back-room-sessions"));
    }

    [Fact]
    public void SellSeatsValidator_RejectsZeroSeats() =>
        Assert.False(new SellSeatsValidator().Validate(new SellSeats("back-room-sessions", 0)).IsValid);

    private Task<int> SeatsAvailableAsync(string eventId) =>
        _db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.Capacity - e.SeatsSold).SingleAsync(TestContext.Current.CancellationToken);
}
