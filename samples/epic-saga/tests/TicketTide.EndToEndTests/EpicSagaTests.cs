namespace TicketTide.EndToEndTests;

/// <summary>
/// Epic Saga: synchronous, atomic, orchestrated.
/// The fan waits for the final answer, and a failure anywhere undoes every step that already succeeded.
/// </summary>
public sealed class EpicSagaTests(TicketTideApp app)
{
    private const string EventId = "midnight-tide";

    [Fact]
    public async Task HappyPath_HoldsSeats_ChargesCard_IssuesTickets_AndEmailsTheFan()
    {
        var fan = Fans.NewEmail();

        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 2, fan));

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(90.00m, order.Amount);
        Assert.Equal(2, order.TicketCodes.Count);

        var hold = await app.Api.GetSeatHoldAsync(order.OrderId);
        Assert.Equal("Held", hold!.Status);

        var payment = await app.Api.GetPaymentAsync(order.OrderId);
        Assert.Equal("Charged", payment!.Status);
        Assert.Equal(90.00m, payment.Amount);

        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Issued", tickets!.Status);
        Assert.Equal(order.TicketCodes, tickets.TicketCodes);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for The Midnight Tide", email.Subject);
        Assert.Contains(order.TicketCodes[0], email.Body);
    }

    [Fact]
    public async Task TicketingFailure_CompensatesNewestFirst_RefundingTheCardAndReleasingTheSeats()
    {
        var fan = Fans.NewEmail();
        var seatsBefore = await app.Api.GetSeatsAvailableAsync(EventId);

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("Ticketing", order.FailureReason);
        Assert.Empty(order.TicketCodes);

        // The compensating actions ran in reverse order: refund first, then release.
        var refunded = order.History.IndexOf("Payments refunded the card.");
        var released = order.History.IndexOf("Inventory released the seats.");
        Assert.True(refunded >= 0 && released > refunded, string.Join(" | ", order.History));

        Assert.Equal("Refunded", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Released", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Equal("Failed", (await app.Api.GetTicketsOrDefaultAsync(order.OrderId))!.Status);

        // Atomic: once the saga is done, it is as if the purchase never happened.
        // (Tests in one class run one at a time, so no other order changes the count meanwhile.)
        Assert.Equal(seatsBefore, await app.Api.GetSeatsAvailableAsync(EventId));

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
    }

    [Fact]
    public async Task PaymentDeclined_ReleasesTheSeats_AndNeverCallsTicketing()
    {
        var fan = Fans.NewEmail();

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 1, fan, PaymentToken: "tok_declined"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("declined", order.FailureReason);
        Assert.Contains("Inventory released the seats.", order.History);

        Assert.Equal("Declined", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Released", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Null(await app.Api.GetTicketsOrDefaultAsync(order.OrderId));
    }

    [Fact]
    public async Task SoldOut_FailsWithoutChargingTheCard()
    {
        // Back Room Sessions only has 4 seats.
        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 5, Fans.NewEmail()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("Not enough seats", order.FailureReason);

        var payment = await app.Api.GetAsync($"/payments/{order.OrderId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, payment.StatusCode);
    }

    [Fact]
    public async Task TwoFansRacingForTheLastSeats_OnlyOneGetsThem()
    {
        // Back Room Sessions has 4 seats. Both fans ask for 3 at the same moment.
        var orders = await Task.WhenAll(
            app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 3, Fans.NewEmail())),
            app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 3, Fans.NewEmail())));

        Assert.Single(orders, o => o.Order.Status == "Confirmed");
        var loser = Assert.Single(orders, o => o.Order.Status == "Failed");
        Assert.Contains("Not enough seats", loser.Order.FailureReason);
        Assert.Equal(1, await app.Api.GetSeatsAvailableAsync("back-room-sessions"));
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

    [Fact]
    public async Task GetOrder_ReturnsTheSagaHistory()
    {
        var (_, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("laugh-track-live", Quantity: 1, Fans.NewEmail()));

        var order = await app.Api.GetOrderAsync(placed.OrderId);

        Assert.Equal("Confirmed", order!.Status);
        Assert.Equal(placed.History, order.History);
    }
}
