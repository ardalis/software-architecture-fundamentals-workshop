namespace TicketTide.EndToEndTests;

/// <summary>
/// The API's behavior, over HTTP. The Clean Architecture and Vertical Slice samples have the same tests, because
/// they have the same behavior: only the code's structure differs.
/// </summary>
public sealed class OrdersApiTests(TicketTideApp app)
{
    private const string EventId = "midnight-tide";

    [Fact]
    public async Task ListEvents_ShowsTheSeedEvents()
    {
        var events = await app.Api.GetFromJsonAsync<List<EventResponse>>("/events", TestContext.Current.CancellationToken);

        Assert.Contains(events!, e => e.Id == "midnight-tide" && e.Capacity == 500);
        Assert.Contains(events!, e => e.Id == "laugh-track-live");
        Assert.Contains(events!, e => e.Id == "back-room-sessions" && e.Capacity == 4);
    }

    [Fact]
    public async Task PlaceOrder_ChargesTheCard_IssuesTickets_AndEmailsTheFan()
    {
        var fan = Fans.NewEmail();
        var seatsBefore = await app.Api.GetSeatsAvailableAsync(EventId);

        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 2, fan));

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("Paid", order!.Status);
        Assert.Equal(90.00m, order.Amount);
        Assert.Equal(2, order.Tickets.Count);
        Assert.All(order.Tickets, t =>
        {
            Assert.Equal(fan, t.HolderEmail);
            Assert.Equal("Paid", t.Status);
        });
        Assert.Equal(seatsBefore - 2, await app.Api.GetSeatsAvailableAsync(EventId));

        var email = await app.Papercut.WaitForMessageAsync(fan);
        Assert.Contains("Your tickets for The Midnight Tide", email.Subject);
        Assert.Contains(order.Tickets[0].Code, email.Body);
    }

    [Fact]
    public async Task PlaceOrder_Declined_Returns422_WithNoTickets_AndTakesNoSeats()
    {
        var seatsBefore = await app.Api.GetSeatsAvailableAsync(EventId);

        var (status, order) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 1, Fans.NewEmail(), PaymentToken: "tok_declined"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Declined", order!.Status);
        Assert.Contains("declined", order.FailureReason);
        Assert.Empty(order.Tickets);
        Assert.Equal(seatsBefore, await app.Api.GetSeatsAvailableAsync(EventId));
    }

    [Fact]
    public async Task PlaceOrder_NotEnoughSeats_Returns400()
    {
        // Back Room Sessions only has 4 seats.
        var (status, _) = await app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 5, Fans.NewEmail()));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task PlaceOrder_UnknownEvent_Returns404()
    {
        var (status, _) = await app.Api.PlaceOrderAsync(new PlaceOrder("no-such-event", Quantity: 1, Fans.NewEmail()));

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task PlaceOrder_InvalidRequest_Returns400()
    {
        var (status, _) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 0, FanEmail: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task PlaceOrder_AddressWeCannotEmail_Returns400()
    {
        // Passes a basic email check, but has a space in it, so no email could ever be delivered.
        var (status, _) = await app.Api.PlaceOrderAsync(new PlaceOrder(EventId, Quantity: 1, FanEmail: "a b@example.com"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task TwoFansRacingForTheLastSeats_OnlyOneGetsThem()
    {
        // Back Room Sessions has 4 seats. Both fans ask for 3 at the same moment. The loser gets 400 (not enough
        // seats) or 409 (someone else saved first, so the card was refunded), depending on the timing.
        var orders = await Task.WhenAll(
            app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 3, Fans.NewEmail())),
            app.Api.PlaceOrderAsync(new PlaceOrder("back-room-sessions", Quantity: 3, Fans.NewEmail())));

        Assert.Single(orders, o => o.Status == HttpStatusCode.Created);
        Assert.Single(orders, o => o.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
        Assert.Equal(1, await app.Api.GetSeatsAvailableAsync("back-room-sessions"));
    }

    [Fact]
    public async Task GetOrder_ReturnsTheOrderAndItsTickets()
    {
        var (_, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("laugh-track-live", Quantity: 1, Fans.NewEmail()));

        var order = await app.Api.GetOrderAsync(placed!.OrderId);

        Assert.Equal("Paid", order!.Status);
        Assert.Equal(placed.Tickets, order.Tickets);
    }

    [Fact]
    public async Task GetOrder_Unknown_Returns404()
    {
        var response = await app.Api.GetAsync($"/orders/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RefundOrder_RefundsEveryTicket_ReturnsTheSeats_AndEmailsTheFan()
    {
        var fan = Fans.NewEmail();
        var (_, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("laugh-track-live", Quantity: 2, fan));
        var seatsBefore = await app.Api.GetSeatsAvailableAsync("laugh-track-live");

        var (status, order) = await app.Api.RefundOrderAsync(placed!.OrderId);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Refunded", order!.Status);
        Assert.All(order.Tickets, t => Assert.Equal("Refunded", t.Status));
        Assert.Equal(seatsBefore + 2, await app.Api.GetSeatsAvailableAsync("laugh-track-live"));
        Assert.Equal("Refunded", (await app.Api.GetOrderAsync(placed.OrderId))!.Status);

        // The fan gets two emails: the tickets, then the refund.
        var email = await app.Papercut.WaitForMessageAsync(fan, subjectContains: "refund");
        Assert.Contains("Your refund for Laugh Track Live", email.Subject);
    }

    [Fact]
    public async Task RefundOrder_Twice_Returns400()
    {
        var (_, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("laugh-track-live", Quantity: 1, Fans.NewEmail()));
        await app.Api.RefundOrderAsync(placed!.OrderId);

        var (status, _) = await app.Api.RefundOrderAsync(placed.OrderId);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task TwoRefundsAtOnce_RefundTheOrderOnce_AndReturnTheSeatsOnce()
    {
        var (_, placed) = await app.Api.PlaceOrderAsync(new PlaceOrder("laugh-track-live", Quantity: 2, Fans.NewEmail()));
        var seatsBefore = await app.Api.GetSeatsAvailableAsync("laugh-track-live");

        var refunds = await Task.WhenAll(app.Api.RefundOrderAsync(placed!.OrderId), app.Api.RefundOrderAsync(placed.OrderId));

        // The loser gets 409 (both read it as paid) or 400 (it read it as already refunded), depending on the timing.
        Assert.Single(refunds, r => r.Status == HttpStatusCode.OK);
        Assert.Single(refunds, r => r.Status is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        Assert.Equal(seatsBefore + 2, await app.Api.GetSeatsAvailableAsync("laugh-track-live"));
    }

    [Fact]
    public async Task RefundOrder_Declined_Returns400()
    {
        var (_, placed) = await app.Api.PlaceOrderAsync(
            new PlaceOrder(EventId, Quantity: 1, Fans.NewEmail(), PaymentToken: "tok_declined"));

        var (status, _) = await app.Api.RefundOrderAsync(placed!.OrderId);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task RefundOrder_Unknown_Returns404()
    {
        var (status, _) = await app.Api.RefundOrderAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, status);
    }
}
