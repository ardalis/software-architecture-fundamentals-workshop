namespace TicketTide.EndToEndTests;

// The tests are black-box: they use their own copies of the API's JSON shapes.

public sealed record PlaceOrder(string EventId, int Quantity, string FanEmail, string PaymentToken = "tok_visa")
{
    /// <summary>This sample's demo switch: Ticketing crashes after writing the tickets.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool SimulateTicketingFailure { get; init; }
}

public sealed record OrderResponse(
    Guid OrderId,
    string Status,
    string EventId,
    string EventName,
    string FanEmail,
    int Quantity,
    decimal Amount,
    string? FailureReason,
    List<TicketResponse> Tickets);

public sealed record TicketResponse(Guid TicketId, string Code, string HolderEmail, string Status);

public sealed record EventResponse(string Id, string Name, string Venue, int Capacity, int SeatsAvailable);

public static class Fans
{
    /// <summary>A unique address per test, so tests can run in parallel and find their own email.</summary>
    public static string NewEmail() => $"fan-{Guid.NewGuid():N}@example.com";
}
