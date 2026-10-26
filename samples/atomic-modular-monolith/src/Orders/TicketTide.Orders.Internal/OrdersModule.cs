using Microsoft.Extensions.DependencyInjection;
using TicketTide.Orders.Internal.Data;
using TicketTide.Orders.Internal.Payments;
using TicketTide.Orders.Internal.UseCases;
using TicketTide.SharedKernel.Data;

namespace TicketTide.Orders.Internal;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        // The module's own DbContext and schema, on the connection every module shares.
        services.AddModuleDbContext<OrdersDbContext>();
        services.AddHostedService<OrdersDatabaseInitializer>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<AfterCommitPublisher>();

        // A singleton, because it remembers every answer by idempotency key, like a real provider does.
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        return services;
    }
}
