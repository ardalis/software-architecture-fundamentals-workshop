namespace TicketTide.EndToEndTests;

/// <summary>
/// Time Travel Saga: synchronous, eventually consistent, choreographed.
/// Each module commits its work and calls the next one. A Ticketing failure is never rolled back:
/// Payments retries until Ticketing recovers, and the data catches up.
/// </summary>
public sealed class TimeTravelSagaTests(TicketTideApp app)
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
        Assert.Equal(1, tickets.Attempts);
        Assert.Equal(order.TicketCodes, tickets.TicketCodes);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for The Midnight Tide", email.Subject);
        Assert.Contains(order.TicketCodes[0], email.Body);
    }

    [Fact]
    public async Task TicketingFailure_PaymentsRetriesInline_AndTheOrderIsConfirmedWithoutRollingBack()
    {
        var fan = Fans.NewEmail();

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 2));

        // Payments retried while the fan waited, so the fan still gets the final answer.
        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(2, order.TicketCodes.Count);
        Assert.Contains("Ticketing issued 2 ticket(s) on attempt 3.", order.History);

        // Nothing was rolled back: the seats stayed held and the card was never refunded.
        Assert.Equal("Held", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Equal("Charged", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.DoesNotContain(order.History, h => h.Contains("refund", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Inventory released the seats.", order.History);

        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Issued", tickets!.Status);
        Assert.Equal(3, tickets.Attempts);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for The Midnight Tide", email.Subject);
    }

    [Fact]
    public async Task LongTicketingOutage_ReturnsPending_ThenPaymentsKeepsRetrying_UntilTheOrderIsConfirmed()
    {
        var fan = Fans.NewEmail();

        // Ticketing fails 8 times: more than the 5 attempts Payments makes while the fan waits.
        var (status, placed) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 8));

        Assert.Equal(HttpStatusCode.Accepted, status);
        Assert.Equal("Pending", placed.Status);
        Assert.Empty(placed.TicketCodes);
        Assert.Contains("Ticketing is still down. Payments will keep retrying in the background.", placed.History);

        // Eventually consistent: for now the seats are held and the card is charged, but there are no tickets.
        Assert.Equal("Held", (await app.Api.GetSeatHoldAsync(placed.OrderId))!.Status);
        Assert.Equal("Charged", (await app.Api.GetPaymentAsync(placed.OrderId))!.Status);

        // Payments keeps retrying in the background, and the data catches up.
        var order = await app.Api.WaitForOrderStatusAsync(placed.OrderId, "Confirmed", TimeSpan.FromSeconds(40));
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(2, order.TicketCodes.Count);
        Assert.Equal("Tickets confirmed.", order.History[^1]);

        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Issued", tickets!.Status);
        Assert.Equal(9, tickets.Attempts);
        Assert.Equal(order.TicketCodes, tickets.TicketCodes);

        Assert.Equal("Charged", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for The Midnight Tide", email.Subject);
        Assert.Contains(order.TicketCodes[0], email.Body);
    }

    [Fact]
    public async Task PaymentDeclined_InventoryReleasesItsSeats_AndTicketingIsNeverCalled()
    {
        var fan = Fans.NewEmail();
        var seatsBefore = await app.Api.GetSeatsAvailableAsync(EventId);

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 1, fan, PaymentToken: "tok_declined"));

        // A declined card is a business answer, not a technical failure, so nobody retries it.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Failed", order.Status);
        Assert.Contains("declined", order.FailureReason);
        Assert.Contains("Inventory released the seats.", order.History);

        Assert.Equal("Declined", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Released", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Null(await app.Api.GetTicketsOrDefaultAsync(order.OrderId));

        // (Tests in one class run one at a time, so no other order changes the count meanwhile.)
        Assert.Equal(seatsBefore, await app.Api.GetSeatsAvailableAsync(EventId));

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
    }

    [Fact]
    public async Task SoldOut_FailsAtInventory_WithoutChargingTheCard()
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
