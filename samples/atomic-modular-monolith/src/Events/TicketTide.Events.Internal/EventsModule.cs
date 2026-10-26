using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TicketTide.Events.Contracts;
using TicketTide.Events.Internal.Data;
using TicketTide.Events.Internal.Integrations;
using TicketTide.SharedKernel.Data;

namespace TicketTide.Events.Internal;

public static class EventsModule
{
    public static IServiceCollection AddEventsModule(this IServiceCollection services)
    {
        // The module's own DbContext and schema, on the connection every module shares.
        services.AddModuleDbContext<EventsDbContext>();
        services.AddHostedService<EventsDatabaseInitializer>();

        // Guards on the module's Contracts, run by the ValidationBehavior.
        services.AddSingleton<IValidator<SellSeats>, SellSeatsValidator>();
        services.AddSingleton<IValidator<ReturnSeats>, ReturnSeatsValidator>();
        return services;
    }
}
