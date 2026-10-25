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
        builder.Services.AddScoped<CheckoutOrchestrator>();
        builder.Services.AddSingleton<OrderCompletionSignal>();
        builder.Services.AddSingleton<IFanNotifier>(_ =>
            new SmtpFanNotifier(SmtpEndpoint.FromConnectionString(builder.Configuration.GetConnectionString("papercut"))));

        // Every reply from the other modules comes back to the orchestrator on the checkout queue.
        builder.Services
            .AddMessageHandler<SeatsHeld, ReplyHandlers>(Queue)
            .AddMessageHandler<SeatsRejected, ReplyHandlers>(Queue)
            .AddMessageHandler<SeatsReleased, ReplyHandlers>(Queue)
            .AddMessageHandler<CardCharged, ReplyHandlers>(Queue)
            .AddMessageHandler<CardDeclined, ReplyHandlers>(Queue)
            .AddMessageHandler<CardRefunded, ReplyHandlers>(Queue)
            .AddMessageHandler<TicketsIssued, ReplyHandlers>(Queue)
            .AddMessageHandler<TicketingFailed, ReplyHandlers>(Queue)
            .AddMessageHandler<TicketsVoided, ReplyHandlers>(Queue);
        return builder;
    }
}
