namespace TicketTide.EndToEndTests;

/// <summary>
/// Fairy Tale Saga: synchronous, eventually consistent, orchestrated.
/// The fan gets an answer right away. When Ticketing fails, nothing is undone: the order is pending,
/// and the orchestrator retries until the tickets are issued.
/// </summary>
public sealed class FairyTaleSagaTests(TicketTideApp app)
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
    public async Task TicketingFailure_ReturnsPending_ThenRetriesUntilTheTicketsAreIssued_WithoutUndoingAnything()
    {
        var fan = Fans.NewEmail();

        var (status, pending) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 2));

        // An honest answer, right away: the order is in, and the tickets are on the way.
        Assert.Equal(HttpStatusCode.Accepted, status);
        Assert.Equal("Pending", pending.Status);
        Assert.Null(pending.FailureReason);
        Assert.Empty(pending.TicketCodes);
        Assert.Contains(pending.History, h => h.StartsWith("Ticketing could not issue the tickets:") && h.EndsWith("Will retry."));

        // The orchestrator owns the error and retries until Ticketing recovers.
        var order = await app.Api.WaitForOrderStatusAsync(pending.OrderId, "Confirmed");
        Assert.Equal(2, order.TicketCodes.Count);
        Assert.Contains(order.History, h => h.StartsWith("Retry 1: Ticketing could not issue the tickets:"));
        Assert.Contains("Retry 2: Ticketing issued 2 ticket(s). Tickets confirmed.", order.History);

        // Nothing was rolled back: the seats are still held and the card was never refunded.
        Assert.DoesNotContain(order.History, h => h.Contains("released") || h.Contains("refunded"));
        Assert.Equal("Held", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Equal("Charged", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);

        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Issued", tickets!.Status);
        Assert.Equal(3, tickets.Attempts);
        Assert.Equal(order.TicketCodes, tickets.TicketCodes);

        // The fan's only email is the confirmation, once the tickets exist.
        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for The Midnight Tide", email.Subject);
        Assert.Contains(order.TicketCodes[0], email.Body);
    }

    [Fact]
    public async Task PaymentDeclined_IsNotRetried_ReleasesTheSeats_AndNeverCallsTicketing()
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

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
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
