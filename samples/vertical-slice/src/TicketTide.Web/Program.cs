using FastEndpoints;
using TicketTide.Web;
using TicketTide.Web.Data;
using TicketTide.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The few things every slice shares: the database, the payment provider, and email.
builder.AddNpgsqlDbContext<AppDbContext>("tickettidedb");
builder.Services.AddHostedService<DatabaseInitializer>();

// A singleton, because it remembers every answer by idempotency key, like a real provider does.
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
builder.Services.AddSingleton<IEmailSender>(sp => new SmtpEmailSender(
    SmtpEndpoint.FromConnectionString(builder.Configuration.GetConnectionString("papercut")),
    sp.GetRequiredService<ILogger<SmtpEmailSender>>()));

// FastEndpoints finds every slice's endpoint in Features/.
builder.Services.AddFastEndpoints();

var app = builder.Build();

app.MapDefaultEndpoints();
// Validation errors are problem details too, like every other error.
app.UseFastEndpoints(c => c.Errors.UseProblemDetails());

// A page at / with buttons for the happy path, a refund, and the failure paths.
app.MapHomePage(
    sampleName: "Vertical Slice",
    summary: "Organized by feature: each request is one folder under Features/ with its own validation and logic.");

app.Run();
