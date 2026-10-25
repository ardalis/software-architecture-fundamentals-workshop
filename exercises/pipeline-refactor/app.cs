#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false

// Run with: dotnet run app.cs
// Then try the requests in README.md.

using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<OrderStore>();
var app = builder.Build();

app.MapPost("/orders", (PlaceOrderRequest request, OrderStore store, ILogger<Program> logger) =>
{
    var sw = Stopwatch.StartNew();
    logger.LogInformation("POST /orders started");
    try
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            logger.LogWarning("POST /orders invalid email");
            return Results.BadRequest(new { error = "A valid email is required." });
        }
        if (request.Quantity is < 1 or > 8)
        {
            logger.LogWarning("POST /orders invalid quantity");
            return Results.BadRequest(new { error = "Quantity must be between 1 and 8." });
        }

        var order = store.Add(request.Email, request.EventCode, request.Quantity);
        logger.LogInformation("POST /orders finished in {Elapsed} ms", sw.ElapsedMilliseconds);
        return Results.Created($"/orders/{order.Id}", order);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "POST /orders failed after {Elapsed} ms", sw.ElapsedMilliseconds);
        return Results.Problem("Something went wrong.");
    }
});

app.MapPost("/orders/{id:int}/transfer", (int id, TransferRequest request, OrderStore store, ILogger<Program> logger) =>
{
    var sw = Stopwatch.StartNew();
    logger.LogInformation("POST /orders/{Id}/transfer started", id);
    try
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail) || !request.ToEmail.Contains('@'))
        {
            logger.LogWarning("POST /orders/{Id}/transfer invalid email", id);
            return Results.BadRequest(new { error = "A valid email is required." });
        }

        var order = store.Transfer(id, request.ToEmail);
        logger.LogInformation("POST /orders/{Id}/transfer finished in {Elapsed} ms", id, sw.ElapsedMilliseconds);
        return Results.Ok(order);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "POST /orders/{Id}/transfer failed", id);
        return Results.Problem("Something went wrong.");
    }
});

app.MapGet("/orders/{id:int}", (int id, OrderStore store) =>
    store.Find(id) is { } order ? Results.Ok(order) : Results.NotFound());

app.Run();

public record PlaceOrderRequest(string Email, string EventCode, int Quantity);
public record TransferRequest(string ToEmail);
public record Order(int Id, string Email, string EventCode, int Quantity);

public class OrderStore
{
    private readonly Dictionary<int, Order> _orders = new();
    private int _nextId = 1;

    public Order Add(string email, string eventCode, int quantity)
    {
        if (eventCode == "BOOM") throw new InvalidOperationException("Simulated failure");
        var order = new Order(_nextId++, email, eventCode, quantity);
        _orders[order.Id] = order;
        return order;
    }

    public Order Transfer(int id, string toEmail)
    {
        if (!_orders.TryGetValue(id, out var order)) throw new KeyNotFoundException();
        return _orders[id] = order with { Email = toEmail };
    }

    public Order? Find(int id) => _orders.GetValueOrDefault(id);
}
