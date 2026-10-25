using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Inventory.Contracts;
using TicketTide.Messaging;
using TicketTide.Payments.Data;
using TicketTide.Payments.Handlers;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments;

public static class PaymentsModule
{
    private const string Queue = "payments";

    public static IHostApplicationBuilder AddPaymentsModule(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<PaymentsDbContext>("paymentsdb");
        builder.Services.AddHostedService<PaymentsDatabaseInitializer>();
        builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();

        builder.Services.AddMessageHandler<SeatsHeld, SeatsHeldHandler>(Queue);
        return builder;
    }
}
