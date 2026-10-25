using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Checkout.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Inventory.Handlers;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Inventory;

public static class InventoryModule
{
    private const string Queue = "inventory";

    public static IHostApplicationBuilder AddInventoryModule(this IHostApplicationBuilder builder)
    {
        // The Inventory module owns its own database. No other module can read it.
        builder.AddNpgsqlDbContext<InventoryDbContext>("inventorydb");
        builder.Services.AddHostedService<InventoryDatabaseInitializer>();

        // ...and its own queue. Inventory starts its step when an order is placed, and must hear about
        // every failure downstream of it: a declined card, a Ticketing failure, and the refund that follows.
        builder.Services
            .AddMessageHandler<OrderPlaced, OrderPlacedHandler>(Queue)
            .AddMessageHandler<PaymentDeclined, ReleaseSeatsHandlers>(Queue)
            .AddMessageHandler<TicketIssueFailed, ReleaseSeatsHandlers>(Queue)
            .AddMessageHandler<CardRefunded, ReleaseSeatsHandlers>(Queue);
        return builder;
    }
}
