namespace TicketTide.EndToEndTests;

/// <summary>
/// What only this sample does: when Ticketing fails part-way through a purchase, one SQL Server transaction rolls back
/// the work of every module. Nothing needs compensating, because nothing was ever committed.
/// </summary>
public sealed class AtomicTransactionTests(TicketTideApp app)
{
    [Fact]
    public async Task TicketingFails_Returns500_AndNoModuleKeepsAnyOfItsWork()
    {
        var fan = Fans.NewEmail();
        var seatsBefore = await app.Api.GetSeatsAvailableAsync("laugh-track-live");

        var response = await app.Api.PostAsJsonAsync(
            "/orders",
            new PlaceOrder("laugh-track-live", Quantity: 2, fan) { SimulateTicketingFailure = true },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("rolled back", problem);
        Assert.Contains("never charged", problem);

        // The Events module had sold the seats inside the transaction. They're back on sale.
        Assert.Equal(seatsBefore, await app.Api.GetSeatsAvailableAsync("laugh-track-live"));

        // And the same fan can buy them straight away: nothing was left half-done.
        var (status, order) = await app.Api.PlaceOrderAsync(new PlaceOrder("laugh-track-live", Quantity: 2, fan));
        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal(2, order!.Tickets.Count);
    }

    [Fact]
    public async Task HomePage_ShowsTheTicketingFailureScenario()
    {
        var html = await app.Api.GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<div class=\"scenario\" hidden>", html); // Hidden in the other monolith samples.
        Assert.Contains("data-scenario=\"ticketingFails\"", html);
    }
}
