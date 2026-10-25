// Fantasy Fiction Saga: asynchronous, atomic, orchestrated.
// One API process (a modular monolith) with a separate database per module, RabbitMQ for the
// messages between modules, and a fake SMTP server.
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");

var checkoutDb = postgres.AddDatabase("checkoutdb");
var inventoryDb = postgres.AddDatabase("inventorydb");
var paymentsDb = postgres.AddDatabase("paymentsdb");
var ticketingDb = postgres.AddDatabase("ticketingdb");

// The management plugin adds a web UI where you can watch the queues.
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var papercut = builder.AddPapercutSmtp("papercut");

builder.AddProject<Projects.TicketTide_Api>("api")
    .WithReference(checkoutDb).WaitFor(checkoutDb)
    .WithReference(inventoryDb).WaitFor(inventoryDb)
    .WithReference(paymentsDb).WaitFor(paymentsDb)
    .WithReference(ticketingDb).WaitFor(ticketingDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(papercut).WaitFor(papercut)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
