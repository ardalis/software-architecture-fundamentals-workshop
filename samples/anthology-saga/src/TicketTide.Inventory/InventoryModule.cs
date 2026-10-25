using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Checkout.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Inventory.Handlers;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;

namespace TicketTide.Inventory;

public static class InventoryModule
{
    private const string Queue = "inventory";

    public static IHostApplicationBuilder AddInventoryModule(this IHostApplicationBuilder builder)
    {
        // The Inventory module owns its own database. No other module can read it.
        builder.AddNpgsqlDbContext<InventoryDbContext>("inventorydb");
        builder.Services.AddHostedService<InventoryDatabaseInitializer>();

        // ...and its own queue, subscribed to the events Inventory reacts to.
        builder.Services.AddMessageHandler<OrderPlaced, OrderPlacedHandler>(Queue);
        builder.Services.AddMessageHandler<PaymentDeclined, PaymentDeclinedHandler>(Queue);
        return builder;
    }
}
