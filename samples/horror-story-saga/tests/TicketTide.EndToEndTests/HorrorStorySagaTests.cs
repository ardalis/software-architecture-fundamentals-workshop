namespace TicketTide.EndToEndTests;

/// <summary>
/// Horror Story Saga: asynchronous, atomic, choreographed.
/// No orchestrator: each module reacts to the event before it, over RabbitMQ. The fan waits for the final answer,
/// and a Ticketing failure is undone by compensations that race each other back up the chain.
/// </summary>
public sealed class HorrorStorySagaTests(TicketTideApp app)
{
    private const string EventId = "midnight-tide";

    [Fact]
    public async Task HappyPath_EventsTravelAlongTheChain_AndTheWaitingFanGetsTheTickets()
    {
        var fan = Fans.NewEmail();

        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 2, fan));

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(90.00m, order.Amount);
        Assert.Equal("The Midnight Tide", order.EventName);
        Assert.Equal(2, order.TicketCodes.Count);

        Assert.Equal("Held", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Equal("Charged", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Issued", tickets!.Status);
        Assert.Equal(1, tickets.Attempts);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for The Midnight Tide", email.Subject);
        Assert.Contains(order.TicketCodes[0], email.Body);
    }

    [Fact]
    public async Task TicketingFailure_IsUndoneByEveryServiceUpstream_BeforeTheFanIsTold()
    {
        var fan = Fans.NewEmail();
        var seatsBefore = await app.Api.GetSeatsAvailableAsync(EventId);

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 1));

        // The fan only hears back once every compensation is in, so nothing below needs to poll.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("Ticketing could not issue the tickets", order.FailureReason);

        Assert.Contains(order.History, h => h.StartsWith("Ticketing could not issue the tickets", StringComparison.Ordinal));
        Assert.Contains("Payments refunded the card.", order.History);
        Assert.Equal("Every compensation is in. Purchase failed.", order.History[^1]);

        // Inventory waited for both the failure and the refund, in whichever order they arrived.
        var released = Assert.Single(order.History, h => h.StartsWith("Inventory released the seats.", StringComparison.Ordinal));
        Assert.Contains("TicketIssueFailed", released);
        Assert.Contains("CardRefunded", released);

        // Atomic: no retries. Ticketing tried once, and every step before it was undone.
        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Failed", tickets!.Status);
        Assert.Equal(1, tickets.Attempts);
        Assert.Equal("Refunded", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Released", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Equal(seatsBefore, await app.Api.GetSeatsAvailableAsync(EventId));

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
    }

    [Fact]
    public async Task PaymentDeclined_ReleasesTheSeats_AndNeverReachesTicketing()
    {
        var fan = Fans.NewEmail();

        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 1, fan, PaymentToken: "tok_declined"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("declined", order.FailureReason);
        Assert.Contains(order.History, h => h.StartsWith("Inventory released the seats.", StringComparison.Ordinal));

        Assert.Equal("Declined", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Released", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Null(await app.Api.GetTicketsOrDefaultAsync(order.OrderId));

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
    }

    [Fact]
    public async Task SoldOut_StopsTheChainAtInventory_SoTheCardIsNeverCharged()
    {
        // Back Room Sessions only has 4 seats.
        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 5, Fans.NewEmail()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("Not enough seats", order.FailureReason);

        Assert.Null(await app.Api.GetPaymentAsync(order.OrderId));
        Assert.Null(await app.Api.GetTicketsOrDefaultAsync(order.OrderId));
    }

    [Fact]
    public async Task UnknownEvent_IsRejectedByInventory()
    {
        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder("no-such-event", Quantity: 1, Fans.NewEmail()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("does not exist", order.FailureReason);
        Assert.Null(await app.Api.GetPaymentAsync(order.OrderId));
    }

    [Fact]
    public async Task InvalidOrder_IsRejectedWithBadRequest()
    {
        var response = await app.Api.PostAsJsonAsync(
            "/orders",
            new PlaceOrder(EventId, Quantity: 0, FanEmail: "not-an-email"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
