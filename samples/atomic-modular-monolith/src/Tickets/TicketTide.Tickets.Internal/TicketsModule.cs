using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TicketTide.SharedKernel.Data;
using TicketTide.Tickets.Contracts;
using TicketTide.Tickets.Internal.Data;
using TicketTide.Tickets.Internal.Integrations;

namespace TicketTide.Tickets.Internal;

public static class TicketsModule
{
    public static IServiceCollection AddTicketsModule(this IServiceCollection services)
    {
        // The module's own DbContext and schema, on the connection every module shares.
        services.AddModuleDbContext<TicketsDbContext>();
        services.AddHostedService<TicketsDatabaseInitializer>();

        // Guards on the module's Contracts, run by the ValidationBehavior.
        services.AddSingleton<IValidator<IssueTickets>, IssueTicketsValidator>();
        return services;
    }
}
