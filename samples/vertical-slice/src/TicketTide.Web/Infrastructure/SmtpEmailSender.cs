using System.Data.Common;
using MailKit.Net.Smtp;
using MimeKit;

namespace TicketTide.Web.Infrastructure;

/// <summary>Sends an email. Each feature writes its own message.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

internal sealed record SmtpEndpoint(string Host, int Port)
{
    // Aspire passes the Papercut connection string as "Endpoint=smtp://host:port".
    public static SmtpEndpoint FromConnectionString(string? connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (!builder.TryGetValue("Endpoint", out var value) || value is not string endpoint)
        {
            throw new InvalidOperationException("The 'papercut' connection string must contain an Endpoint.");
        }

        var uri = new Uri(endpoint, UriKind.Absolute);
        return new SmtpEndpoint(uri.Host, uri.Port);
    }
}

/// <summary>
/// Sends email through SMTP. In development the SMTP server is Papercut, which catches every message.
/// Email is best effort: the order is already saved, so a failed email is logged instead of failing the request.
/// </summary>
internal sealed partial class SmtpEmailSender(SmtpEndpoint smtp, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private const string From = "tickets@tickettide.example";

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            await SendOrThrowAsync(to, subject, body, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogEmailFailed(logger, ex, subject, to);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send the email \"{Subject}\" to {To}.")]
    private static partial void LogEmailFailed(ILogger logger, Exception exception, string subject, string to);

    private async Task SendOrThrowAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("TicketTide", From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, MailKit.Security.SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
