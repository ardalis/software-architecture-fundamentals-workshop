using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Inventory.Data;

namespace TicketTide.Inventory;

public static class InventoryModule
{
    public static IHostApplicationBuilder AddInventoryModule(this IHostApplicationBuilder builder)
    {
        // The Inventory module owns its own database. No other module can read it.
        builder.AddNpgsqlDbContext<InventoryDbContext>("inventorydb");
        builder.Services.AddHostedService<InventoryDatabaseInitializer>();
        return builder;
    }
}
