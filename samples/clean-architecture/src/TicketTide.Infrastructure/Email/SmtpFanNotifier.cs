using System.Data.Common;
using System.Globalization;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using TicketTide.Core.Interfaces;
using TicketTide.Core.OrderAggregate;

namespace TicketTide.Infrastructure.Email;

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
/// Emails the fan through SMTP. In development the SMTP server is Papercut, which catches every message.
/// Email is best effort: the order is already saved, so a failed email is logged instead of failing the request.
/// </summary>
internal sealed partial class SmtpFanNotifier(SmtpEndpoint smtp, ILogger<SmtpFanNotifier> logger) : IFanNotifier
{
    private const string From = "tickets@tickettide.example";

    public Task SendTicketsAsync(Order order, CancellationToken cancellationToken) =>
        SendAsync(
            order.FanEmail,
            $"Your tickets for {order.EventName}",
            $"""
            Thanks for your order!

            Order: {order.Id}
            Event: {order.EventName}
            Tickets: {string.Join(", ", order.Tickets.Select(t => t.Code))}
            Total charged: {FormatMoney(order.Amount)}
            """,
            cancellationToken);

    public Task SendRefundAsync(Order order, CancellationToken cancellationToken) =>
        SendAsync(
            order.FanEmail,
            $"Your refund for {order.EventName}",
            $"""
            Your order has been refunded, and its tickets no longer work.

            Order: {order.Id}
            Refunded: {FormatMoney(order.Amount)}
            """,
            cancellationToken);

    // TicketTide sells in US dollars. Format the same way whatever the server's culture is.
    private static string FormatMoney(decimal amount) => amount.ToString("$#,##0.00", CultureInfo.InvariantCulture);

    private async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
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
