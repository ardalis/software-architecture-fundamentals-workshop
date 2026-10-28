# NetArchTest fitness functions (exercise shape)

From `exercises/fitness-functions/fitness.cs`: a .NET 10 file-based app (`dotnet run fitness.cs`) that exits 1 on any failure, so it runs as a CI step as-is.

```csharp
#:package NetArchTest.Rules@1.3.2
using System.Diagnostics;
using System.Reflection;
using NetArchTest.Rules;
using TicketTide.Core.Orders;

var assembly = Assembly.GetExecutingAssembly();
var failures = 0;

// ADR-0003: Core has no infrastructure dependencies
Check("Core does not depend on Infrastructure",
    Types.InAssembly(assembly)
        .That().ResideInNamespace("TicketTide.Core")
        .ShouldNot().HaveDependencyOn("TicketTide.Infrastructure")
        .GetResult());

Check("IOrderRepository implementations reside in Infrastructure",
    Types.InAssembly(assembly)
        .That().ImplementInterface(typeof(IOrderRepository)).And().AreClasses()
        .Should().ResideInNamespace("TicketTide.Infrastructure")
        .GetResult());

Check("Billing does not depend on Orders",
    Types.InAssembly(assembly)
        .That().ResideInNamespace("TicketTide.Core.Billing")
        .ShouldNot().HaveDependencyOn("TicketTide.Core.Orders")
        .GetResult());

var sw = Stopwatch.StartNew();
for (var i = 0; i < 100_000; i++) PriceCalculator.Total(3, 89.50m);
sw.Stop();
CheckThat("Pricing stays fast", sw.ElapsedMilliseconds < 200, $"{sw.ElapsedMilliseconds} ms");

return failures == 0 ? 0 : 1;

void Check(string name, TestResult result)
{
    if (result.IsSuccessful) { Console.WriteLine($"PASS  {name}"); return; }
    failures++;
    Console.WriteLine($"FAIL  {name}");
    foreach (var type in result.FailingTypeNames ?? []) Console.WriteLine($"        {type}");
}

void CheckThat(string name, bool passed, string detail)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name} ({detail})");
    if (!passed) failures++;
}
```

Typical code fixes (not rule edits):

- Core news up `SqlOrderRepository`: inject `IOrderRepository` through the constructor and wire the implementation in the host.
- Billing depends on Orders (`Order`, `PriceCalculator`), forming a cycle: (a) Billing owns its input type and pricing is passed in or moved, or (b) Orders raises an event Billing handles. (b) survives extracting Billing into its own service.

Timing checks are noisy in CI: warm up, take the median of several runs, use generous margins, or move them to a perf stage with BenchmarkDotNet.
