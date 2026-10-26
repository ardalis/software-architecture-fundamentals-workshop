// Atomic Modular Monolith: one web app hosting four modules, ONE SQL Server database they all share, and a fake
// SMTP server. Sharing the database is what lets one transaction cover every module.
var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql").AddDatabase("tickettidedb");

var papercut = builder.AddPapercutSmtp("papercut");

builder.AddProject<Projects.TicketTide_Web>("api")
    .WithReference(db).WaitFor(db)
    .WithReference(papercut).WaitFor(papercut)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
