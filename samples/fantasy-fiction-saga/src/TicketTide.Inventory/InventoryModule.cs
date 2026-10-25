using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Inventory.Contracts;
using TicketTide.Inventory.Data;
using TicketTide.Inventory.Handlers;
using TicketTide.Messaging;

namespace TicketTide.Inventory;

public static class InventoryModule
{
    private const string Queue = "inventory";

    public static IHostApplicationBuilder AddInventoryModule(this IHostApplicationBuilder builder)
    {
        // The Inventory module owns its own database. No other module can read it.
        builder.AddNpgsqlDbContext<InventoryDbContext>("inventorydb");
        builder.Services.AddHostedService<InventoryDatabaseInitializer>();

        // ...and its own queue.
        builder.Services.AddMessageHandler<HoldSeats, HoldSeatsHandler>(Queue);
        builder.Services.AddMessageHandler<ReleaseSeats, ReleaseSeatsHandler>(Queue);
        return builder;
    }
}
