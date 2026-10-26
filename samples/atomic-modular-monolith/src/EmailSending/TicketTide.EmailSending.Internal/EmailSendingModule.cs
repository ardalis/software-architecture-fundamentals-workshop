using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicketTide.EmailSending.Contracts;
using TicketTide.EmailSending.Internal.Integrations;

namespace TicketTide.EmailSending.Internal;

public static class EmailSendingModule
{
    /// <param name="smtpConnectionString">Papercut's connection string, from Aspire.</param>
    public static IServiceCollection AddEmailSendingModule(this IServiceCollection services, string? smtpConnectionString)
    {
        // No database: this module only talks to the SMTP server, and never joins a transaction.
        var smtp = SmtpEndpoint.FromConnectionString(smtpConnectionString);
        services.AddSingleton<IEmailSender>(sp => new SmtpEmailSender(smtp, sp.GetRequiredService<ILogger<SmtpEmailSender>>()));

        // Guards on the module's Contracts, run by the ValidationBehavior.
        services.AddSingleton<IValidator<SendEmail>, SendEmailValidator>();
        return services;
    }
}
