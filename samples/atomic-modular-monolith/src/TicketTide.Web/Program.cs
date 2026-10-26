using FastEndpoints;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketTide.EmailSending.Contracts;
using TicketTide.EmailSending.Internal;
using TicketTide.Events.Contracts;
using TicketTide.Events.Internal;
using TicketTide.Orders.Contracts;
using TicketTide.Orders.Internal;
using TicketTide.SharedKernel.Behaviors;
using TicketTide.SharedKernel.Data;
using TicketTide.Tickets.Contracts;
using TicketTide.Tickets.Internal;
using TicketTide.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ONE database, and one connection per request (a scoped SqlConnection, with Aspire's health check and tracing).
// Every module's DbContext uses that connection, so the unit of work can put all of them in one transaction.
builder.AddSqlServerClient("tickettidedb");
builder.Services.AddSharedDatabase<SqlConnection>((options, connection) => options.UseSqlServer(connection));

// A modular monolith: one process, four modules. Each module registers its own services.
builder.Services.AddEventsModule();
builder.Services.AddOrdersModule();
builder.Services.AddTicketsModule();
builder.Services.AddEmailSendingModule(builder.Configuration.GetConnectionString("papercut"));

// Modules talk to each other in-process through the Mediator messages in their Contracts projects. The source
// generator scans these assemblies for messages (Contracts) and handlers (Internal). Every message goes through
// the behaviors, in this order: logging (outermost), then validation.
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies =
    [
        typeof(GetEventDetails), typeof(EventsModule),
        typeof(OrderPlaced), typeof(OrdersModule),
        typeof(IssueTickets), typeof(TicketsModule),
        typeof(SendEmail), typeof(EmailSendingModule),
    ];
    options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
});

// Each module also owns its HTTP endpoints.
builder.Services.AddFastEndpoints(options => options.Assemblies =
[
    typeof(EventsModule).Assembly,
    typeof(OrdersModule).Assembly,
]);

var app = builder.Build();

app.MapDefaultEndpoints();
// Validation errors are problem details too, like every other error.
app.UseFastEndpoints(c => c.Errors.UseProblemDetails());

// A page at / with buttons for the happy path, a refund, the failure paths, and a failure that rolls back.
app.MapHomePage(
    sampleName: "Atomic Modular Monolith",
    summary: "Organized by module: Events, Orders, Tickets, and EmailSending, each with its own Contracts. " +
        "They share one SQL Server database, so one transaction covers all of them.",
    ticketingFailureOutcome: "Tickets crashes after writing the tickets. One rollback undoes the seats and the tickets, " +
        "the card is never charged, and there's nothing to compensate.");

app.Run();
