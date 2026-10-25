using FastEndpoints;
using TicketTide.Api;
using TicketTide.Checkout;
using TicketTide.Inventory;
using TicketTide.Inventory.Contracts;
using TicketTide.Messaging;
using TicketTide.Payments;
using TicketTide.Ticketing;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The saga's messages travel over RabbitMQ, even though every module runs in this one process.
builder.AddMessaging();

// A modular monolith: one process, four modules, and each module owns its own database and queue.
builder.AddCheckoutModule();
builder.AddInventoryModule();
builder.AddPaymentsModule();
builder.AddTicketingModule();

// Checkout prices the order with one synchronous, in-process Mediator query to Inventory.
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(GetEventPrice), typeof(InventoryModule)];
});

// Each module also owns its HTTP endpoints.
builder.Services.AddFastEndpoints(options => options.Assemblies =
[
    typeof(CheckoutModule).Assembly,
    typeof(InventoryModule).Assembly,
    typeof(PaymentsModule).Assembly,
    typeof(TicketingModule).Assembly,
]);

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseFastEndpoints();

// A page at / with buttons for the happy path and the failure paths.
app.MapHomePage(
    sampleName: "Fantasy Fiction Saga",
    summary: "Asynchronous, atomic, orchestrated.",
    diagram: "fantasy-fiction-saga",
    ticketingFailures: 1,
    ticketingFailureOutcome: "The orchestrator undoes every step, including replies still in flight, and the fan gets the final answer.");

app.Run();
