using FastEndpoints;
using TicketTide.Api;
using TicketTide.Checkout;
using TicketTide.Inventory;
using TicketTide.Inventory.Contracts;
using TicketTide.Payments;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing;
using TicketTide.Ticketing.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// A modular monolith: one process, four modules, and each module owns its own database.
builder.AddCheckoutModule();
builder.AddInventoryModule();
builder.AddPaymentsModule();
builder.AddTicketingModule();

// Modules talk to each other in-process through the Mediator commands in their Contracts projects.
// Phone Tag: each module's handler calls the next module (Checkout -> Inventory -> Payments -> Ticketing).
// The source generator scans these assemblies for messages (Contracts) and handlers (modules).
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies =
    [
        typeof(HoldSeats), typeof(InventoryModule),
        typeof(ChargeCard), typeof(PaymentsModule),
        typeof(IssueTickets), typeof(TicketingModule),
    ];
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
    sampleName: "Phone Tag Saga",
    summary: "Synchronous, atomic, choreographed.",
    diagram: "phone-tag-saga",
    ticketingFailures: 1,
    ticketingFailureOutcome: "The failure travels back along the chain, and each module undoes its own step.");

app.Run();
