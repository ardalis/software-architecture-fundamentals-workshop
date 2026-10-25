#:package NetArchTest.Rules@1.3.2

// Run with: dotnet run fitness.cs
// The fitness functions are at the top. The TicketTide code they protect is below.
// Add your rules to the "Your fitness functions" section.

using System.Diagnostics;
using System.Reflection;
using NetArchTest.Rules;
using TicketTide.Core.Orders;

var assembly = Assembly.GetExecutingAssembly();
var failures = 0;

// ---- Example fitness function ------------------------------------------------

Check("Web does not depend on Infrastructure",
    Types.InAssembly(assembly)
        .That().ResideInNamespace("TicketTide.Web")
        .ShouldNot().HaveDependencyOn("TicketTide.Infrastructure")
        .GetResult());

// ---- Your fitness functions --------------------------------------------------
// See README.md for the rules to add.


// ------------------------------------------------------------------------------

Console.WriteLine(failures == 0 ? "All fitness functions passed." : $"{failures} fitness function(s) failed.");
return failures == 0 ? 0 : 1;

void Check(string name, TestResult result)
{
    if (result.IsSuccessful)
    {
        Console.WriteLine($"PASS  {name}");
        return;
    }
    failures++;
    Console.WriteLine($"FAIL  {name}");
    foreach (var type in result.FailingTypeNames ?? [])
        Console.WriteLine($"        {type}");
}

// Use CheckThat for fitness functions that aren't NetArchTest rules, such as timing checks.
#pragma warning disable CS8321 // unused until you write one
void CheckThat(string name, bool passed, string detail)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name} ({detail})");
    if (!passed) failures++;
}

// ==============================================================================
// TicketTide code under test
// ==============================================================================

namespace TicketTide.Core.Orders
{
    using TicketTide.Core.Billing;
    using TicketTide.Infrastructure;

    public record Order(int Id, string Email, int Quantity, decimal UnitPrice);

    public interface IOrderRepository
    {
        void Save(Order order);
        Order? Find(int id);
    }

    public class OrderService
    {
        private readonly IOrderRepository _repository = new SqlOrderRepository();
        private readonly InvoiceService _invoices = new();

        public Order Place(int id, string email, int quantity, decimal unitPrice)
        {
            var order = new Order(id, email, quantity, unitPrice);
            _repository.Save(order);
            _invoices.CreateInvoice(order);
            return order;
        }
    }

    public static class PriceCalculator
    {
        public static decimal Total(int quantity, decimal unitPrice)
        {
            var subtotal = quantity * unitPrice;
            var fee = Math.Round(subtotal * 0.07m + quantity * 1.25m, 2);
            return subtotal + fee;
        }
    }
}

namespace TicketTide.Core.Billing
{
    using TicketTide.Core.Orders;

    public record Invoice(int OrderId, decimal Amount);

    public class InvoiceService
    {
        private readonly List<Invoice> _invoices = new();

        public Invoice CreateInvoice(Order order)
        {
            var invoice = new Invoice(order.Id, PriceCalculator.Total(order.Quantity, order.UnitPrice));
            _invoices.Add(invoice);
            return invoice;
        }
    }
}

namespace TicketTide.Infrastructure
{
    using TicketTide.Core.Orders;

    public class SqlOrderRepository : IOrderRepository
    {
        private readonly Dictionary<int, Order> _rows = new();
        public void Save(Order order) => _rows[order.Id] = order;
        public Order? Find(int id) => _rows.GetValueOrDefault(id);
    }
}

namespace TicketTide.Web
{
    using TicketTide.Core.Orders;

    public class OrderEndpoints(OrderService orders)
    {
        public Order Post(int id, string email, int quantity) => orders.Place(id, email, quantity, 89.50m);
    }
}
