using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Checkout.Data;
using TicketTide.Checkout.Notifications;
using TicketTide.Checkout.Saga;
using TicketTide.Inventory.Contracts;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Checkout;

public static class CheckoutModule
{
    private const string Queue = "checkout";

    public static IHostApplicationBuilder AddCheckoutModule(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<CheckoutDbContext>("checkoutdb");
        builder.Services.AddHostedService<CheckoutDatabaseInitializer>();
        builder.Services.AddSingleton<IFanNotifier>(_ =>
            new SmtpFanNotifier(SmtpEndpoint.FromConnectionString(builder.Configuration.GetConnectionString("papercut"))));

        // Checkout listens to the other modules' events on its own queue. It does not tell them what to do:
        // it only records what they did, and answers the fan when the saga ends.
        builder.Services
            .AddMessageHandler<SeatsHeld, OrderEventHandlers>(Queue)
            .AddMessageHandler<SeatsRejected, OrderEventHandlers>(Queue)
            .AddMessageHandler<SeatsReleased, OrderEventHandlers>(Queue)
            .AddMessageHandler<PaymentReceived, OrderEventHandlers>(Queue)
            .AddMessageHandler<PaymentDeclined, OrderEventHandlers>(Queue)
            .AddMessageHandler<TicketsIssued, OrderEventHandlers>(Queue);
        return builder;
    }
}
