namespace TicketTide.EndToEndTests;

public static class ApiExtensions
{
    public static async Task<(HttpStatusCode Status, OrderResponse Order)> PlaceOrderAsync(this HttpClient api, PlaceOrder request)
    {
        var response = await api.PostAsJsonAsync("/orders", request, TestContext.Current.CancellationToken);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken);
        return (response.StatusCode, order!);
    }

    public static Task<OrderResponse?> GetOrderAsync(this HttpClient api, Guid orderId) =>
        api.GetFromJsonAsync<OrderResponse>($"/orders/{orderId}", TestContext.Current.CancellationToken);

    /// <summary>Polls the order until it reaches <paramref name="status"/>: the data catches up eventually.</summary>
    public static async Task<OrderResponse> WaitForOrderStatusAsync(this HttpClient api, Guid orderId, string status, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        var order = await api.GetOrderAsync(orderId);
        while (order!.Status != status && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
            order = await api.GetOrderAsync(orderId);
        }

        return order;
    }

    public static async Task<int> GetSeatsAvailableAsync(this HttpClient api, string eventId)
    {
        var events = await api.GetFromJsonAsync<List<EventResponse>>("/inventory/events", TestContext.Current.CancellationToken);
        return events!.Single(e => e.Id == eventId).SeatsAvailable;
    }

    public static Task<SeatHoldResponse?> GetSeatHoldAsync(this HttpClient api, Guid orderId) =>
        api.GetFromJsonAsync<SeatHoldResponse>($"/inventory/holds/{orderId}", TestContext.Current.CancellationToken);

    public static Task<PaymentResponse?> GetPaymentAsync(this HttpClient api, Guid orderId) =>
        api.GetFromJsonAsync<PaymentResponse>($"/payments/{orderId}", TestContext.Current.CancellationToken);

    public static async Task<TicketsResponse?> GetTicketsOrDefaultAsync(this HttpClient api, Guid orderId)
    {
        var response = await api.GetAsync($"/tickets/{orderId}", TestContext.Current.CancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await response.Content.ReadFromJsonAsync<TicketsResponse>(TestContext.Current.CancellationToken);
    }
}
