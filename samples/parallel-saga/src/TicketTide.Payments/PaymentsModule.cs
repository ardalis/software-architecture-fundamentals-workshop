using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
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

        builder.Services.AddMessageHandler<ChargeCard, ChargeCardHandler>(Queue);
        builder.Services.AddMessageHandler<RefundCard, RefundCardHandler>(Queue);
        return builder;
    }
}
