using Ardalis.Result;
using TicketTide.Events.Contracts;
using TicketTide.Orders.Internal;
using TicketTide.Orders.Internal.Data;

namespace TicketTide.Orders.Tests;

public sealed class OrderTests
{
    private const string Fan = "fan@example.com";

    private static readonly EventDetails MidnightTide = new("midnight-tide", "The Midnight Tide", 45m, SeatsAvailable: 500);

    [Fact]
    public void UsesOnlyOtherModulesContracts() => ModuleBoundaries.AssertUsesOnlyContracts(typeof(OrdersModule).Assembly);

    [Fact]
    public void Place_PricesTheOrder()
    {
        var order = Order.Place(MidnightTide, 2, Fan);

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(90m, order.Amount);
        Assert.Equal("The Midnight Tide", order.EventName);
    }

    [Fact]
    public void MarkPaid_KeepsThePaymentReference()
    {
        var order = Order.Place(MidnightTide, 1, Fan);

        order.MarkPaid("ch_123");

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal("ch_123", order.PaymentReference);
    }

    [Fact]
    public void MarkPaid_Twice_Throws()
    {
        var order = Order.Place(MidnightTide, 1, Fan);
        order.MarkPaid("ch_123");

        Assert.Throws<InvalidOperationException>(() => order.MarkPaid("ch_456"));
    }

    [Fact]
    public void MarkDeclined_KeepsTheReason()
    {
        var order = Order.Place(MidnightTide, 1, Fan);

        order.MarkDeclined("The card was declined.");

        Assert.Equal(OrderStatus.Declined, order.Status);
        Assert.Equal("The card was declined.", order.FailureReason);
    }

    [Fact]
    public void Refund_APaidOrder_ChangesItsVersion()
    {
        var order = Order.Place(MidnightTide, 1, Fan);
        order.MarkPaid("ch_123");
        var version = order.Version;

        var result = order.Refund();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.NotEqual(version, order.Version); // So a second request refunding the same order is a conflict.
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

        Assert.Contains("Only a paid order", Assert.Single(result.ValidationErrors).ErrorMessage);
    }
}
