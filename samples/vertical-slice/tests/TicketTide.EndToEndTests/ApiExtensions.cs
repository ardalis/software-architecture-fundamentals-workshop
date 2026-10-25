namespace TicketTide.EndToEndTests;

public static class ApiExtensions
{
    public static async Task<(HttpStatusCode Status, OrderResponse? Order)> PlaceOrderAsync(this HttpClient api, PlaceOrder request)
    {
        var response = await api.PostAsJsonAsync("/orders", request, TestContext.Current.CancellationToken);
        return (response.StatusCode, await ReadOrderAsync(response));
    }

    public static async Task<(HttpStatusCode Status, OrderResponse? Order)> RefundOrderAsync(this HttpClient api, Guid orderId)
    {
        var response = await api.PostAsync($"/orders/{orderId}/refund", content: null, TestContext.Current.CancellationToken);
        return (response.StatusCode, await ReadOrderAsync(response));
    }

    public static Task<OrderResponse?> GetOrderAsync(this HttpClient api, Guid orderId) =>
        api.GetFromJsonAsync<OrderResponse>($"/orders/{orderId}", TestContext.Current.CancellationToken);

    public static async Task<int> GetSeatsAvailableAsync(this HttpClient api, string eventId)
    {
        var events = await api.GetFromJsonAsync<List<EventResponse>>("/events", TestContext.Current.CancellationToken);
        return events!.Single(e => e.Id == eventId).SeatsAvailable;
    }

    // Only successful and declined orders have an order in the body. Errors have FastEndpoints' error shape.
    private static async Task<OrderResponse?> ReadOrderAsync(HttpResponseMessage response) =>
        response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.UnprocessableEntity
            ? await response.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken)
            : null;
}
