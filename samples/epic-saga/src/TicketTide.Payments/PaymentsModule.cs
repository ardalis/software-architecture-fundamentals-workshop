using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Payments.Data;
using TicketTide.Payments.Provider;

namespace TicketTide.Payments;

public static class PaymentsModule
{
    public static IHostApplicationBuilder AddPaymentsModule(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<PaymentsDbContext>("paymentsdb");
        builder.Services.AddHostedService<PaymentsDatabaseInitializer>();
        builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
        return builder;
    }
}
