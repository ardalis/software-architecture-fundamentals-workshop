namespace TicketTide.EndToEndTests;

/// <summary>
/// Phone Tag Saga: synchronous, atomic, choreographed.
/// The fan waits while each module calls the next one. A failure travels back along the chain,
/// and each module undoes its own step on the way.
/// </summary>
public sealed class PhoneTagSagaTests(TicketTideApp app)
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

        // Every module reported its step back up the chain, in the order the calls were made.
        Assert.Equal(
            [
                "Order placed.",
                "Inventory held 2 seat(s) for The Midnight Tide.",
                "Payments charged $90.00.",
                "Ticketing issued 2 ticket(s).",
                "Tickets confirmed.",
            ],
            order.History);

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
    public async Task TicketingFailure_TravelsBackAlongTheChain_RefundingTheCardThenReleasingTheSeats()
    {
        var fan = Fans.NewEmail();
        var seatsBefore = await app.Api.GetSeatsAvailableAsync(EventId);

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("Ticketing", order.FailureReason);
        Assert.Empty(order.TicketCodes);

        // The failure went back up the chain: Ticketing failed, then Payments refunded, then Inventory released.
        var ticketingFailed = order.History.FindIndex(h => h.StartsWith("Ticketing could not issue the tickets", StringComparison.Ordinal));
        var refunded = order.History.IndexOf("Payments refunded the card.");
        var released = order.History.IndexOf("Inventory released the seats.");
        Assert.True(ticketingFailed >= 0 && refunded > ticketingFailed && released > refunded, string.Join(" | ", order.History));

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
        Assert.DoesNotContain("Payments refunded the card.", order.History);

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

        // Inventory failed its own step, so it never called Payments.
        var payment = await app.Api.GetAsync($"/payments/{order.OrderId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, payment.StatusCode);
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
