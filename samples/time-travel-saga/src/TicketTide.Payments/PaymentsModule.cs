using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Payments.Data;
using TicketTide.Payments.Handoffs;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments;

public static class PaymentsModule
{
    public static IHostApplicationBuilder AddPaymentsModule(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<PaymentsDbContext>("paymentsdb");
        builder.Services.AddHostedService<PaymentsDatabaseInitializer>();
        builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();

        // Registered after the database initializer, so the tables exist before the first retry.
        builder.Services.AddHostedService<TicketHandoffRetrier>();
        return builder;
    }
}
