using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Messaging;
using TicketTide.Ticketing.Contracts;
using TicketTide.Ticketing.Data;
using TicketTide.Ticketing.Handlers;

namespace TicketTide.Ticketing;

public static class TicketingModule
{
    private const string Queue = "ticketing";

    public static IHostApplicationBuilder AddTicketingModule(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<TicketingDbContext>("ticketingdb");
        builder.Services.AddHostedService<TicketingDatabaseInitializer>();

        builder.Services.AddMessageHandler<IssueTickets, IssueTicketsHandler>(Queue);
        builder.Services.AddMessageHandler<VoidTickets, VoidTicketsHandler>(Queue);
        return builder;
    }
}
