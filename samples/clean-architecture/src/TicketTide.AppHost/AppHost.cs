// Clean Architecture: one web app, one database, and a fake SMTP server.
var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("postgres").AddDatabase("tickettidedb");

var papercut = builder.AddPapercutSmtp("papercut");

builder.AddProject<Projects.TicketTide_Web>("api")
    .WithReference(db).WaitFor(db)
    .WithReference(papercut).WaitFor(papercut)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
