using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Ticketing.Data;

namespace TicketTide.Ticketing;

public static class TicketingModule
{
    public static IHostApplicationBuilder AddTicketingModule(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<TicketingDbContext>("ticketingdb");
        builder.Services.AddHostedService<TicketingDatabaseInitializer>();
        return builder;
    }
}
