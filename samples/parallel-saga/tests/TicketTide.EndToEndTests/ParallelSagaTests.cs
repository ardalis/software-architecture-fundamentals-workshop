namespace TicketTide.EndToEndTests;

/// <summary>
/// Parallel Saga: asynchronous, eventually consistent, orchestrated.
/// The fan gets an answer right away; the three steps run in parallel over RabbitMQ; Ticketing failures are retried.
/// </summary>
public sealed class ParallelSagaTests(TicketTideApp app)
{
    private const string EventId = "midnight-tide";

    [Fact]
    public async Task HappyPath_AnswersAtOnce_ThenHoldsChargesAndIssuesInParallel_AndEmailsTheTickets()
    {
        var fan = Fans.NewEmail();

        var (status, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 2, fan));

        Assert.Equal(HttpStatusCode.Accepted, status);
        Assert.Equal("Pending", placed.Status);
        Assert.Equal(90.00m, placed.Amount);

        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);
        Assert.Equal("Confirmed", order.Status);
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
    public async Task TicketingFailure_IsRetriedByTheOrchestrator_WithoutUndoingAnything()
    {
        var fan = Fans.NewEmail();

        var (status, placed) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 2, fan, SimulateTicketingFailures: 2));
        Assert.Equal(HttpStatusCode.Accepted, status);

        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);

        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(2, order.History.Count(h => h.StartsWith("Ticketing failed", StringComparison.Ordinal)));
        Assert.Contains(order.History, h => h.StartsWith("Retry 2:", StringComparison.Ordinal));

        // Eventually consistent: nothing was rolled back while Ticketing was down.
        Assert.Equal("Charged", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);
        Assert.Equal("Held", (await app.Api.GetSeatHoldAsync(order.OrderId))!.Status);
        Assert.Equal(3, (await app.Api.GetTicketsOrDefaultAsync(order.OrderId))!.Attempts);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for", email.Subject);
    }

    [Fact]
    public async Task PaymentDeclined_FailsTheOrder_AndCompensatesTheStepsThatRanInParallel()
    {
        var fan = Fans.NewEmail();

        var (_, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 1, fan, PaymentToken: "tok_declined"));
        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);

        Assert.Equal("Failed", order.Status);
        Assert.Contains("declined", order.FailureReason);
        Assert.Equal("Declined", (await app.Api.GetPaymentAsync(order.OrderId))!.Status);

        // Inventory and Ticketing may already have done their part. Their work is undone.
        var hold = await ApiExtensions.EventuallyAsync(() => app.Api.GetSeatHoldAsync(order.OrderId), h => h?.Status == "Released");
        Assert.Equal("Released", hold!.Status);
        var tickets = await ApiExtensions.EventuallyAsync(() => app.Api.GetTicketsOrDefaultAsync(order.OrderId), t => t?.Status == "Voided");
        Assert.Equal("Voided", tickets!.Status);

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("could not be completed", email.Subject);
    }

    [Fact]
    public async Task SoldOut_FailsTheOrder_AndRefundsTheCardThatWasChargedInParallel()
    {
        // Back Room Sessions only has 4 seats. Payments charges the card before Inventory says no.
        var (_, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 5, Fans.NewEmail()));
        var order = await app.Api.WaitForOrderToFinishAsync(placed.OrderId);

        Assert.Equal("Failed", order.Status);
        Assert.Contains("Not enough seats", order.FailureReason);

        var payment = await ApiExtensions.EventuallyAsync(
            () => app.Api.GetPaymentAsync(order.OrderId), p => p?.Status is "Refunded" or "Cancelled");
        Assert.True(payment!.Status is "Refunded" or "Cancelled", $"Payment is {payment.Status}");
        var tickets = await ApiExtensions.EventuallyAsync(() => app.Api.GetTicketsOrDefaultAsync(order.OrderId), t => t?.Status == "Voided");
        Assert.Equal("Voided", tickets!.Status);
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
}
