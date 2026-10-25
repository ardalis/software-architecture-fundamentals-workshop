using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Notifications;
using TicketTide.Checkout.Saga;

namespace TicketTide.Checkout;

public static class CheckoutModule
{
    public static IHostApplicationBuilder AddCheckoutModule(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<CheckoutDbContext>("checkoutdb");
        builder.Services.AddHostedService<CheckoutDatabaseInitializer>();
        builder.Services.AddScoped<CheckoutStep>();
        builder.Services.AddSingleton<IFanNotifier>(_ =>
            new SmtpFanNotifier(SmtpEndpoint.FromConnectionString(builder.Configuration.GetConnectionString("papercut"))));
        return builder;
    }
}
