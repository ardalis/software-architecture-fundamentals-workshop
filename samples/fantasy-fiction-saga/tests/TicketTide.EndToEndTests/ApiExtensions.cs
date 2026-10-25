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

    /// <summary>
    /// Polls the order until the saga finishes (Confirmed or Failed), like a fan refreshing the page.
    /// Only needed when <c>POST /orders</c> gave up waiting and answered <c>202 Accepted</c>.
    /// </summary>
    public static async Task<OrderResponse> WaitForOrderToFinishAsync(this HttpClient api, Guid orderId, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            var order = await api.GetOrderAsync(orderId);
            if (order!.Status is not ("Pending" or "Compensating"))
            {
                return order;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Order {orderId} is still {order.Status}: {string.Join(" | ", order.History)}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Polls until <paramref name="condition"/> holds.</summary>
    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            var value = await read();
            if (condition(value) || DateTimeOffset.UtcNow > deadline)
            {
                return value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }
    }

    public static async Task<int> GetSeatsAvailableAsync(this HttpClient api, string eventId)
    {
        var events = await api.GetFromJsonAsync<List<EventResponse>>("/inventory/events", TestContext.Current.CancellationToken);
        return events!.Single(e => e.Id == eventId).SeatsAvailable;
    }

    // Each module may not have seen the order yet, so these return null for 404 Not Found.
    public static Task<SeatHoldResponse?> GetSeatHoldAsync(this HttpClient api, Guid orderId) =>
        api.GetOrDefaultAsync<SeatHoldResponse>($"/inventory/holds/{orderId}");

    public static Task<PaymentResponse?> GetPaymentAsync(this HttpClient api, Guid orderId) =>
        api.GetOrDefaultAsync<PaymentResponse>($"/payments/{orderId}");

    public static Task<TicketsResponse?> GetTicketsOrDefaultAsync(this HttpClient api, Guid orderId) =>
        api.GetOrDefaultAsync<TicketsResponse>($"/tickets/{orderId}");

    private static async Task<T?> GetOrDefaultAsync<T>(this HttpClient api, string path)
    {
        var response = await api.GetAsync(path, TestContext.Current.CancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken);
    }
}
