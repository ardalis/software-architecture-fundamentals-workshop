using FastEndpoints;
using TicketTide.Core.Services;
using TicketTide.Infrastructure;
using TicketTide.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The composition root: Web is the one project that sees Core and Infrastructure, and wires them together.
builder.AddInfrastructure();
builder.Services.AddScoped<IPlaceOrderService, PlaceOrderService>();
builder.Services.AddScoped<IRefundOrderService, RefundOrderService>();

builder.Services.AddFastEndpoints();

var app = builder.Build();

app.MapDefaultEndpoints();
// Validation errors are problem details too, like every other error.
app.UseFastEndpoints(c => c.Errors.UseProblemDetails());

// A page at / with buttons for the happy path, a refund, and the failure paths.
app.MapHomePage(
    sampleName: "Clean Architecture",
    summary: "Organized by layer: the business rules live on the aggregates in Core, and Web and Infrastructure depend on Core.");

app.Run();
