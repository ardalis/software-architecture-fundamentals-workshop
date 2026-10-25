using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketTide.Core.Interfaces;
using TicketTide.Infrastructure.Data;
using TicketTide.Infrastructure.Email;
using TicketTide.Infrastructure.Payments;

namespace TicketTide.Infrastructure;

public static class InfrastructureServiceExtensions
{
    /// <summary>Registers the implementations of Core's interfaces.</summary>
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<AppDbContext>("tickettidedb");
        builder.Services.AddHostedService<DatabaseInitializer>();

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        builder.Services.AddScoped<IEventRepository, EventRepository>();
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();

        // A singleton, because it remembers every answer by idempotency key, like a real provider does.
        builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        builder.Services.AddSingleton<IFanNotifier>(sp => new SmtpFanNotifier(
            SmtpEndpoint.FromConnectionString(builder.Configuration.GetConnectionString("papercut")),
            sp.GetRequiredService<ILogger<SmtpFanNotifier>>()));

        return builder;
    }
}
