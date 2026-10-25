using Ardalis.Result;
using TicketTide.Core.EventAggregate;
using TicketTide.Core.OrderAggregate;

namespace TicketTide.UnitTests;

public sealed class OrderTests
{
    private const string Fan = "fan@example.com";

    private static readonly Event MidnightTide =
        new("midnight-tide", "The Midnight Tide", "Harbor Club", DateTimeOffset.UtcNow, 45m, capacity: 500);

    [Fact]
    public void Place_PricesTheOrder_WithNoTicketsYet()
    {
        var order = Order.Place(MidnightTide, 2, Fan);

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(90m, order.Amount);
        Assert.Equal("The Midnight Tide", order.EventName);
        Assert.Empty(order.Tickets);
    }

    [Fact]
    public void MarkPaid_IssuesOneTicketPerSeat_HeldByTheFan()
    {
        var order = Order.Place(MidnightTide, 3, Fan);

        order.MarkPaid("ch_123");

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(3, order.Tickets.Count);
        Assert.All(order.Tickets, t =>
        {
            Assert.Equal(Fan, t.HolderEmail);
            Assert.Equal(TicketStatus.Paid, t.Status);
            Assert.StartsWith("TT-", t.Code);
        });
        Assert.Equal(3, order.Tickets.Select(t => t.Code).Distinct().Count());
    }

    [Fact]
    public void MarkDeclined_IssuesNoTickets_AndKeepsTheReason()
    {
        var order = Order.Place(MidnightTide, 2, Fan);

        order.MarkDeclined("The card was declined.");

        Assert.Equal(OrderStatus.Declined, order.Status);
        Assert.Equal("The card was declined.", order.FailureReason);
        Assert.Empty(order.Tickets);
    }

    [Fact]
    public void Refund_APaidOrder_RefundsEveryTicket()
    {
        var order = Order.Place(MidnightTide, 2, Fan);
        order.MarkPaid("ch_123");

        var result = order.Refund();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.All(order.Tickets, t => Assert.Equal(TicketStatus.Refunded, t.Status));
    }

    [Fact]
    public void Refund_ADeclinedOrder_IsInvalid()
    {
        var order = Order.Place(MidnightTide, 1, Fan);
        order.MarkDeclined("The card was declined.");

        var result = order.Refund();

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(OrderStatus.Declined, order.Status);
    }

    [Fact]
    public void Refund_Twice_IsInvalid()
    {
        var order = Order.Place(MidnightTide, 1, Fan);
        order.MarkPaid("ch_123");
        order.Refund();

        var result = order.Refund();

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("Only a paid order", Assert.Single(result.ValidationErrors).ErrorMessage);
    }

    [Fact]
    public void MarkPaid_Twice_Throws()
    {
        var order = Order.Place(MidnightTide, 1, Fan);
        order.MarkPaid("ch_123");

        Assert.Throws<InvalidOperationException>(() => order.MarkPaid("ch_456"));
    }
}
