namespace TicketTide.EndToEndTests;

/// <summary>
/// Anthology Saga: asynchronous, eventually consistent, choreographed.
/// The fan gets an answer right away; the modules react to each other's events over RabbitMQ with no orchestrator;
/// a Ticketing failure is retried by redelivering its message, and nothing upstream is undone.
/// </summary>
public sealed class AnthologySagaTests(TicketTideApp app)
{
    private const string EventId = "midnight-tide";

    [Fact]
    public async Task HappyPath_AnswersAtOnce_ThenEachModuleReactsToTheLastOne_AndEmailsTheTickets()
    {
        var fan = Fans.NewEmail();

        var (status, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 2, fan));

        Assert.Equal(HttpStatusCode.Accepted, status);
        Assert.Equal("Pending", placed.Status);
        Assert.Null(placed.Amount); // Inventory prices the order later, when it reacts to Order Placed.

        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal("The Midnight Tide", order.EventName);
        Assert.Equal(90.00m, order.Amount);
        Assert.Equal(2, order.TicketCodes.Count);
        Assert.Contains("Payments charged $90.00.", order.History);

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
    public async Task TicketingFailure_IsRetriedByRedelivery_WithoutUndoingAnything()
    {
        var fan = Fans.NewEmail();

        var (status, placed) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 3));
        Assert.Equal(HttpStatusCode.Accepted, status);
        Assert.Equal("Pending", placed.Status);

        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);
        Assert.Equal("Confirmed", order.Status);

        // RabbitMQ delivered Payment Received four times: three failures, then success.
        var tickets = await app.Api.GetTicketsOrDefaultAsync(order.OrderId);
        Assert.Equal("Issued", tickets!.Status);
        Assert.Equal(4, tickets.Attempts);
        Assert.Null(tickets.LastError);

        // Eventually consistent: nothing upstream was undone while Ticketing was failing.
        Assert.Equal("Charged", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Held", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.DoesNotContain(order.History, h => h.Contains("released", StringComparison.OrdinalIgnoreCase));

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for", email.Subject);
    }

    [Fact]
    public async Task PaymentDeclined_FailsTheOrder_AndInventoryReleasesTheSeats()
    {
        var fan = Fans.NewEmail();

        var (status, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 1, fan, PaymentToken: "tok_declined"));
        Assert.Equal(HttpStatusCode.Accepted, status);

        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);

        Assert.Equal("Failed", order.Status);
        Assert.Contains("declined", order.FailureReason);
        Assert.Equal("Declined", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);

        // A business "no" is not retried. Inventory reacts to Payment Declined by releasing the seats it held.
        var hold = await ApiExtensions.EventuallyAsync(() => app.Api.GetSeatHoldAsync(order.OrderId), h => h?.Status == "Released");
        Assert.Equal("Released", hold!.Status);
        var history = await ApiExtensions.EventuallyAsync(
            async () => (await app.Api.GetOrderAsync(order.OrderId))!.History, h => h.Contains("Inventory released the seats."));
        Assert.Contains("Inventory released the seats.", history);

        // Ticketing only reacts to Payment Received, so it never heard of this order.
        Assert.Null(await app.Api.GetTicketsOrDefaultAsync(order.OrderId));

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
    }

    [Fact]
    public async Task SoldOut_FailsTheOrder_BeforeTheCardIsCharged()
    {
        // Back Room Sessions only has 4 seats. Inventory publishes Seats Rejected, and Payments never hears of the order.
        var (status, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 5, Fans.NewEmail()));
        Assert.Equal(HttpStatusCode.Accepted, status);

        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);

        Assert.Equal("Failed", order.Status);
        Assert.Contains("Not enough seats", order.FailureReason);
        Assert.Null(await app.Api.GetSeatHoldAsync(order.OrderId));
        Assert.Null(await app.Api.GetPaymentAsync(order.OrderId));
        Assert.Null(await app.Api.GetTicketsOrDefaultAsync(order.OrderId));
    }

    [Fact]
    public async Task UnknownEvent_IsAccepted_ThenRejectedByInventory()
    {
        // Checkout does not know the catalog. Only Inventory does, and it answers with an event.
        var (status, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("no-such-event", Quantity: 1, Fans.NewEmail()));
        Assert.Equal(HttpStatusCode.Accepted, status);

        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);

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
