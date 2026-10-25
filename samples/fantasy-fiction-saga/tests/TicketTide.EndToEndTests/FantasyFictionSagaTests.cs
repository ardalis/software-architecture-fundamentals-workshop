namespace TicketTide.EndToEndTests;

/// <summary>
/// Fantasy Fiction Saga: asynchronous, atomic, orchestrated.
/// The three steps run in parallel over RabbitMQ, yet the fan waits for the final answer, and any failure
/// undoes every step. When <c>POST /orders</c> returns, every reply is in and every compensation is done.
/// </summary>
public sealed class FantasyFictionSagaTests(TicketTideApp app)
{
    private const string EventId = "midnight-tide";

    [Fact]
    public async Task HappyPath_WaitsForAllThreeReplies_ThenConfirms_AndEmailsTheTickets()
    {
        var fan = Fans.NewEmail();

        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 2, fan));

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(90.00m, order.Amount);
        Assert.Equal(2, order.TicketCodes.Count);

        // All three replies are in the history, in whatever order they arrived.
        Assert.Contains("Inventory held 2 seat(s) for The Midnight Tide.", order.History);
        Assert.Contains("Payments charged $90.00.", order.History);
        Assert.Contains("Ticketing issued 2 ticket(s).", order.History);

        Assert.Equal("Held", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Equal("Charged", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Issued", tickets!.Status);
        Assert.Equal(order.TicketCodes, tickets.TicketCodes);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for The Midnight Tide", email.Subject);
        Assert.Contains(order.TicketCodes[0], email.Body);
    }

    [Fact]
    public async Task TicketingFailure_UndoesEveryStep_BeforeTheFanGetsAnAnswer()
    {
        var fan = Fans.NewEmail();
        var seatsBefore = await app.Api.GetSeatsAvailableAsync(EventId);

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("Ticketing", order.FailureReason);
        Assert.Empty(order.TicketCodes);

        // Whether Inventory and Payments replied before or after Ticketing failed, both steps were undone,
        // and the orchestrator waited for both confirmations.
        Assert.Contains("Payments refunded the card.", order.History);
        Assert.Contains("Inventory released the seats.", order.History);
        Assert.Equal("Every step is undone. Purchase failed.", order.History[^1]);

        // No polling needed: an atomic saga only answers once the compensations are done.
        Assert.Equal("Refunded", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Released", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Failed", tickets!.Status);
        Assert.Equal(1, tickets.Attempts); // No retries.

        // Atomic: once the saga is done, it is as if the purchase never happened.
        // (Tests in one class run one at a time, so no other order changes the count meanwhile.)
        Assert.Equal(seatsBefore, await app.Api.GetSeatsAvailableAsync(EventId));

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
    }

    [Fact]
    public async Task PaymentDeclined_ReleasesTheSeats_AndVoidsTheTicketsThatWereIssuedInParallel()
    {
        var fan = Fans.NewEmail();

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 1, fan, PaymentToken: "tok_declined"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("declined", order.FailureReason);
        Assert.Contains("Inventory released the seats.", order.History);
        Assert.Contains("Ticketing voided the tickets.", order.History);

        Assert.Equal("Declined", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Released", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Equal("Voided", (await app.Api.GetTicketsOrDefaultAsync(order.OrderId))!.Status);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
    }

    [Fact]
    public async Task SoldOut_RefundsTheCardThatWasChargedInParallel_AndVoidsTheTickets()
    {
        // Back Room Sessions only has 4 seats. Payments and Ticketing do their part before or after Inventory says no.
        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 5, Fans.NewEmail()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("Not enough seats", order.FailureReason);

        // The orchestrator only sends Refund Card after Payments replied "charged", so the card is always refunded.
        Assert.Equal("Refunded", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Voided", (await app.Api.GetTicketsOrDefaultAsync(order.OrderId))!.Status);
    }

    [Fact]
    public async Task UnknownEvent_IsRejectedBeforeTheSagaStarts()
    {
        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder("no-such-event", Quantity: 1, Fans.NewEmail()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Null(await app.Api.GetTicketsOrDefaultAsync(order.OrderId));
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
    public async Task GetOrder_ReturnsTheSameFinalStateTheFanWaitedFor()
    {
        var (_, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("laugh-track-live", Quantity: 1, Fans.NewEmail()));

        var order = await app.Api.GetOrderAsync(placed.OrderId);

        Assert.Equal("Confirmed", order!.Status);
        Assert.Equal(placed.History, order.History);
    }
}
