// Fairy Tale Saga: synchronous, eventually consistent, orchestrated.
// One API process (a modular monolith) with a separate database per module, plus a fake SMTP server.
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");

var checkoutDb = postgres.AddDatabase("checkoutdb");
var inventoryDb = postgres.AddDatabase("inventorydb");
var paymentsDb = postgres.AddDatabase("paymentsdb");
var ticketingDb = postgres.AddDatabase("ticketingdb");

var papercut = builder.AddPapercutSmtp("papercut");

builder.AddProject<Projects.TicketTide_Api>("api")
    .WithReference(checkoutDb).WaitFor(checkoutDb)
    .WithReference(inventoryDb).WaitFor(inventoryDb)
    .WithReference(paymentsDb).WaitFor(paymentsDb)
    .WithReference(ticketingDb).WaitFor(ticketingDb)
    .WithReference(papercut).WaitFor(papercut)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
