using Ardalis.Result;
using TicketTide.Core.EventAggregate;

namespace TicketTide.UnitTests;

public sealed class EventTests
{
    private static Event BackRoom() => new("back-room-sessions", "Back Room Sessions", "The Back Room", DateTimeOffset.UtcNow, 30m, capacity: 4);

    [Fact]
    public void SellSeats_TakesThemOffSale()
    {
        var evt = BackRoom();

        var result = evt.SellSeats(3);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, evt.SeatsAvailable);
    }

    [Fact]
    public void SellSeats_MoreThanAreLeft_IsInvalid_AndSellsNothing()
    {
        var evt = BackRoom();

        var result = evt.SellSeats(5);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("Not enough seats", Assert.Single(result.ValidationErrors).ErrorMessage);
        Assert.Equal(4, evt.SeatsAvailable);
    }

    [Fact]
    public void ReturnSeats_MoreThanWereSold_Throws()
    {
        var evt = BackRoom();
        evt.SellSeats(1);

        Assert.Throws<InvalidOperationException>(() => evt.ReturnSeats(2));
    }

    [Fact]
    public void ReturnSeats_PutsThemBackOnSale()
    {
        var evt = BackRoom();
        evt.SellSeats(4);

        evt.ReturnSeats(2);

        Assert.Equal(2, evt.SeatsAvailable);
    }
}
