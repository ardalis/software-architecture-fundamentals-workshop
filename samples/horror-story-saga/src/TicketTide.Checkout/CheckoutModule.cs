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
        builder.Services.AddSingleton<OrderCompletionSignal>();
        builder.Services.AddSingleton<IFanNotifier>(_ =>
            new SmtpFanNotifier(SmtpEndpoint.FromConnectionString(builder.Configuration.GetConnectionString("papercut"))));

        // Checkout coordinates nothing, but it listens to every event so the order's history tells the whole story.
        // Only three of them finish the order: Tickets Issued, Seats Rejected, and Seats Released.
        builder.Services
            .AddMessageHandler<SeatsHeld, OrderEventHandlers>(Queue)
            .AddMessageHandler<SeatsRejected, OrderEventHandlers>(Queue)
            .AddMessageHandler<SeatsReleased, OrderEventHandlers>(Queue)
            .AddMessageHandler<PaymentReceived, OrderEventHandlers>(Queue)
            .AddMessageHandler<PaymentDeclined, OrderEventHandlers>(Queue)
            .AddMessageHandler<CardRefunded, OrderEventHandlers>(Queue)
            .AddMessageHandler<TicketsIssued, OrderEventHandlers>(Queue)
            .AddMessageHandler<TicketIssueFailed, OrderEventHandlers>(Queue);
        return builder;
    }
}
