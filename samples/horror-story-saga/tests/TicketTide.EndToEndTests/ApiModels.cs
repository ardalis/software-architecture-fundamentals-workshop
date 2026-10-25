namespace TicketTide.EndToEndTests;

// The tests are black-box: they use their own copies of the API's JSON shapes.

public sealed record PlaceOrder(
    string EventId,
    int Quantity,
    string FanEmail,
    string PaymentToken = "tok_visa",
    int SimulateTicketingFailures = 0);

public sealed record OrderResponse(
    Guid OrderId,
    string Status,
    string EventId,
    string? EventName,
    int Quantity,
    decimal? Amount,
    List<string> TicketCodes,
    string? FailureReason,
    List<string> History);

public sealed record EventResponse(string Id, string Name, int Capacity, int SeatsAvailable);

public sealed record SeatHoldResponse(Guid OrderId, string EventId, int Quantity, decimal Amount, string Status);

public sealed record PaymentResponse(Guid OrderId, decimal Amount, string Status, string? FailureReason);

public sealed record TicketsResponse(Guid OrderId, string Status, int Attempts, List<string> TicketCodes, string? LastError);

public static class Fans
{
    /// <summary>A unique address per test, so tests can run in parallel and find their own email.</summary>
    public static string NewEmail() => $"fan-{Guid.NewGuid():N}@example.com";
}
