using Ardalis.Result;
using TicketTide.Orders.Internal.Data;
using TicketTide.Orders.Internal.UseCases.GetOrder;
using TicketTide.Orders.Internal.UseCases.PlaceOrder;
using TicketTide.Orders.Internal.UseCases.RefundOrder;

namespace TicketTide.IntegrationTests;

/// <summary>
/// Three modules change data when a fan buys tickets, Events, Tickets, and Orders, and they do it in one database
/// transaction. These tests show that a failure at any step undoes ALL of it, with no compensating actions: nothing
/// was ever committed, so there's nothing to undo.
/// </summary>
public sealed class TransactionTests : IAsyncLifetime
{
    private const string BackRoom = "back-room-sessions"; // 4 seats at $30.
    private const string Fan = "fan@example.com";

    private readonly TestModules _modules = new();

    public async ValueTask InitializeAsync() => await _modules.StartAsync();

    public ValueTask DisposeAsync() => _modules.DisposeAsync();

    [Fact]
    public async Task PlaceOrder_CommitsEveryModulesWork_ThenEmailsTheFan()
    {
        var result = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 3, Fan, "tok_visa"));

        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.Equal("Paid", order.Status);
        Assert.Equal(3, await _modules.SeatsSoldAsync(BackRoom));                         // Events
        Assert.Equal(3, (await _modules.TicketsAsync(order.OrderId)).Count);               // Tickets
        Assert.Equal(OrderStatus.Paid, (await _modules.OrderAsync(order.OrderId))!.Status); // Orders
        Assert.Equal(90m, Assert.Single(_modules.Payments.Charges));

        // After the commit, the Tickets module handled OrderPlaced and emailed the tickets.
        var email = Assert.Single(_modules.Emails);
        Assert.Equal("Your tickets for Back Room Sessions", email.Subject);
        Assert.All(order.Tickets, t => Assert.Contains(t.Code, email.Body));
    }

    [Fact]
    public async Task PlaceOrder_TicketingFails_RollsBackEveryModule_AndTheCardIsNeverCharged()
    {
        var result = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 3, Fan, "tok_visa", SimulateTicketingFailure: true));

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Contains("Tickets failed, so the whole purchase was rolled back", Assert.Single(result.Errors));

        // Events had already sold the seats and Tickets had written the tickets. The rollback undid both.
        Assert.Equal(0, await _modules.SeatsSoldAsync(BackRoom));
        Assert.Equal(0, await _modules.OrderCountAsync());
        Assert.Empty(_modules.Payments.Charges);
        Assert.Empty(_modules.Payments.Refunds); // Nothing to compensate.
        Assert.Empty(_modules.Emails);

        // And the seats really are back: someone else can buy all four.
        var next = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 4, Fan, "tok_visa"));
        Assert.True(next.IsSuccess);
    }

    [Fact]
    public async Task PlaceOrder_Declined_RollsBackTheSeatsAndTickets_AndSavesOnlyTheDeclinedOrder()
    {
        var result = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 2, Fan, "tok_declined"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Declined", result.Value.Status);
        Assert.Empty(result.Value.Tickets);
        Assert.Equal(0, await _modules.SeatsSoldAsync(BackRoom));
        Assert.Empty(await _modules.TicketsAsync(result.Value.OrderId));
        Assert.Equal(OrderStatus.Declined, (await _modules.OrderAsync(result.Value.OrderId))!.Status);
        Assert.Empty(_modules.Emails);
    }

    [Fact]
    public async Task PlaceOrder_SavingTheOrderFailsAfterTheCharge_RollsBackEverything_AndRefundsTheCard()
    {
        _modules.SaveFailures.FailNextOrderInsert = true;

        var result = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 2, Fan, "tok_visa"));

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Contains("the card was refunded", Assert.Single(result.Errors));
        Assert.Equal(0, await _modules.SeatsSoldAsync(BackRoom));
        Assert.Equal(0, await _modules.OrderCountAsync());

        // The payment provider is outside the database, so it's the one thing that needs compensating.
        Assert.Single(_modules.Payments.Charges);
        Assert.Single(_modules.Payments.Refunds);
    }

    [Fact]
    public async Task PlaceOrder_NotEnoughSeats_IsInvalid_AndNeverChargesTheCard()
    {
        var result = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 5, Fan, "tok_visa"));

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_modules.Payments.Charges);
    }

    [Fact]
    public async Task PlaceOrder_UnknownEvent_IsNotFound()
    {
        var result = await _modules.SendAsync(new PlaceOrderCommand("no-such-event", 1, Fan, "tok_visa"));

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetOrder_ReturnsTheOrder_WithTheTicketsModulesTickets()
    {
        var placed = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 2, Fan, "tok_visa"));

        var result = await _modules.QueryAsync(new GetOrderQuery(placed.Value.OrderId));

        Assert.Equal(placed.Value.Tickets, result.Value.Tickets);
    }

    [Fact]
    public async Task RefundOrder_ChangesEveryModuleTogether_ThenRefundsTheCard()
    {
        var placed = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 3, Fan, "tok_visa"));

        var result = await _modules.SendAsync(new RefundOrderCommand(placed.Value.OrderId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Refunded", result.Value.Status);
        Assert.Equal(0, await _modules.SeatsSoldAsync(BackRoom));
        Assert.All(await _modules.TicketsAsync(placed.Value.OrderId), t => Assert.Equal("Refunded", t.Status.ToString()));
        Assert.Single(_modules.Payments.Refunds);
        Assert.Contains(_modules.Emails, e => e.Subject == "Your refund for Back Room Sessions");
    }

    [Fact]
    public async Task RefundOrder_ThePaymentProviderFails_RollsBackEveryModule()
    {
        var placed = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 3, Fan, "tok_visa"));
        _modules.Payments.RefundsFail = true;

        var result = await _modules.SendAsync(new RefundOrderCommand(placed.Value.OrderId));

        // The order, its tickets, and its seats had all changed inside the transaction. None of it was committed.
        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Equal(OrderStatus.Paid, (await _modules.OrderAsync(placed.Value.OrderId))!.Status);
        Assert.All(await _modules.TicketsAsync(placed.Value.OrderId), t => Assert.Equal("Paid", t.Status.ToString()));
        Assert.Equal(3, await _modules.SeatsSoldAsync(BackRoom));

        // So support can simply try again.
        _modules.Payments.RefundsFail = false;
        Assert.True((await _modules.SendAsync(new RefundOrderCommand(placed.Value.OrderId))).IsSuccess);
    }

    [Fact]
    public async Task RefundOrder_Twice_IsInvalid()
    {
        var placed = await _modules.SendAsync(new PlaceOrderCommand(BackRoom, 1, Fan, "tok_visa"));
        await _modules.SendAsync(new RefundOrderCommand(placed.Value.OrderId));

        var result = await _modules.SendAsync(new RefundOrderCommand(placed.Value.OrderId));

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Single(_modules.Payments.Refunds);
    }
}
