using FastEndpoints;
using TicketTide.Api;
using TicketTide.Checkout;
using TicketTide.Inventory;
using TicketTide.Messaging;
using TicketTide.Payments;
using TicketTide.Ticketing;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The saga's events travel over RabbitMQ, even though every module runs in this one process.
builder.AddMessaging();

// A modular monolith: one process, four modules, and each module owns its own database and queue.
// There is no orchestrator. Each module subscribes to the events it reacts to and publishes its own.
builder.AddCheckoutModule();
builder.AddInventoryModule();
builder.AddPaymentsModule();
builder.AddTicketingModule();

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
    sampleName: "Anthology Saga",
    summary: "Asynchronous, eventually consistent, choreographed.",
    diagram: "anthology-saga",
    ticketingFailures: 5,
    ticketingFailureOutcome: "The fan gets Pending at once, Ticketing's message is retried through RabbitMQ, and the tickets arrive later.");

app.Run();
