using System.Data.Common;
using MailKit.Net.Smtp;
using MimeKit;
using TicketTide.Checkout.Data;

namespace TicketTide.Checkout.Notifications;

// Public only so the TicketsIssued handler (Mediator handlers must be public) can depend on it.
public interface IFanNotifier
{
    Task SendConfirmationAsync(Order order, CancellationToken cancellationToken);

    Task SendFailureAsync(Order order, CancellationToken cancellationToken);
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

/// <summary>Emails the fan through SMTP. In development the SMTP server is Papercut, which catches every message.</summary>
internal sealed class SmtpFanNotifier(SmtpEndpoint smtp) : IFanNotifier
{
    private const string From = "tickets@tickettide.example";

    public Task SendConfirmationAsync(Order order, CancellationToken cancellationToken) =>
        SendAsync(
            order.FanEmail,
            $"Your tickets for {order.EventName}",
            $"""
            Thanks for your order!

            Order: {order.Id}
            Event: {order.EventName}
            Tickets: {string.Join(", ", order.TicketCodes)}
            Total charged: {Money.Format(order.Amount ?? 0m)}
            """,
            cancellationToken);

    public Task SendFailureAsync(Order order, CancellationToken cancellationToken) =>
        SendAsync(
            order.FanEmail,
            "Your TicketTide order could not be completed",
            $"""
            Sorry, we could not complete your order, and you have not been charged.

            Order: {order.Id}
            Reason: {order.FailureReason}
            """,
            cancellationToken);

    private async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
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
