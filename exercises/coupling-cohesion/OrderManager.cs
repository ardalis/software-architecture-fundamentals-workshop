// Run with: dotnet run OrderManager.cs
// This code works. Your job is to find what makes it hard to change.

var manager = new OrderManager();
var order = manager.PlaceOrder("fan@example.com", "ARENA-2026-07-14", 4, 89.50m, true, false, true);
Console.WriteLine($"Order {order.Id} status {order.Status}, total {order.Total:C}");

var report = new SettlementReport();
Console.WriteLine($"Venue fee for order: {report.VenueFee(order):C}");

manager.Cancel(order);
Console.WriteLine($"Order {order.Id} status {order.Status}");

public class Order
{
    public int Id;
    public string Email = "";
    public string EventCode = "";
    public int Quantity;
    public decimal Total;
    public int Status; // 0 = new, 1 = paid, 2 = cancelled, 3 = refunded
    public DateTime PlacedAt;
}

public class OrderManager
{
    public static List<Order> AllOrders = new();
    private static int _nextId = 1;

    public Order PlaceOrder(string email, string eventCode, int quantity, decimal price,
        bool sendEmail, bool isComp, bool applyFee)
    {
        if (!email.Contains('@')) throw new ArgumentException("bad email");

        var order = new Order
        {
            Id = _nextId++,
            Email = email,
            EventCode = eventCode,
            Quantity = quantity,
            PlacedAt = DateTime.Now,
        };

        var subtotal = isComp ? 0 : quantity * price;
        var fee = applyFee ? Math.Round(subtotal * 0.07m + quantity * 1.25m, 2) : 0;
        var tax = eventCode.StartsWith("ARENA") ? subtotal * 0.06m : subtotal * 0.04m;
        order.Total = subtotal + fee + tax;

        order.Status = 1;
        AllOrders.Add(order);

        if (sendEmail)
        {
            var body = $"<h1>Thanks!</h1><p>You bought {quantity} tickets for {eventCode}.</p>";
            new SmtpEmailClient("smtp.tickettide.local", 25).Send(email, "Your tickets", body);
        }

        File.AppendAllText("orders.log", $"{DateTime.Now:O} placed {order.Id}{Environment.NewLine}");
        return order;
    }

    public void Cancel(Order order)
    {
        if (order.Status == 1 && (DateTime.Now - order.PlacedAt).TotalHours < 24)
        {
            order.Status = 2;
        }
        else if (order.Status == 1)
        {
            order.Status = 3;
        }
        File.AppendAllText("orders.log", $"{DateTime.Now:O} cancelled {order.Id}{Environment.NewLine}");
    }

    public string FormatSeatLabel(string section, int row, int seat) => $"{section}-{row:D2}-{seat:D3}";

    public decimal ConvertToEuros(decimal amount) => amount * 0.92m;
}

public class SettlementReport
{
    public decimal VenueFee(Order order)
    {
        // Recalculate the subtotal the same way checkout did.
        var subtotal = order.Total / (order.EventCode.StartsWith("ARENA") ? 1.06m : 1.04m);
        var fee = Math.Round(subtotal * 0.07m + order.Quantity * 1.25m, 2);
        return order.Status == 1 ? fee : 0;
    }

    public int PaidOrderCount() => OrderManager.AllOrders.Count(o => o.Status == 1);
}

public class SmtpEmailClient(string host, int port)
{
    public void Send(string to, string subject, string body) =>
        Console.WriteLine($"[smtp {host}:{port}] to={to} subject={subject} ({body.Length} chars)");
}
