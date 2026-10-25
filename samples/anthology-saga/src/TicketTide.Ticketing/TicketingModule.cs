using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
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

        builder.Services.AddMessageHandler<PaymentReceived, PaymentReceivedHandler>(Queue);
        return builder;
    }
}
