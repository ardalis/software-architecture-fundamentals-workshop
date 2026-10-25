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
// There is no orchestrator and no in-process call between modules: each module only reacts to events.
builder.AddMessaging();

// A modular monolith: one process, four modules, and each module owns its own database and queue.
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
    sampleName: "Horror Story Saga",
    summary: "Asynchronous, atomic, choreographed.",
    diagram: "horror-story-saga",
    ticketingFailures: 1,
    ticketingFailureOutcome: "Ticketing's failure event reaches every upstream module, and their compensations race.");

app.Run();
