using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;
using TicketTide.SharedKernel.Data;

namespace TicketTide.SharedKernel.Behaviors;

/// <summary>
/// Logs every Mediator message, and starts a trace span for it, saying whether it ran inside the unit of work's
/// transaction. In the Aspire dashboard, one <c>POST /orders</c> shows Events, Tickets, and Orders all working
/// inside the same transaction, on the same connection.
/// </summary>
public sealed partial class LoggingBehavior<TMessage, TResponse>(UnitOfWork unitOfWork, ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TMessage).Name;
        var inTransaction = unitOfWork.InTransaction;

        using var activity = ModuleTelemetry.Source.StartActivity(name);
        activity?.SetTag("tickettide.message", typeof(TMessage).FullName);
        activity?.SetTag("tickettide.in_transaction", inTransaction);

        LogHandling(logger, name, inTransaction ? "inside the transaction" : "outside any transaction");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await next(message, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogFailed(logger, ex, name, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            LogHandled(logger, name, stopwatch.ElapsedMilliseconds);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {Message} {Transaction}")]
    private static partial void LogHandling(ILogger logger, string message, string transaction);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {Message} in {ElapsedMs} ms")]
    private static partial void LogHandled(ILogger logger, string message, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message} failed after {ElapsedMs} ms")]
    private static partial void LogFailed(ILogger logger, Exception exception, string message, long elapsedMs);
}

public static class ModuleTelemetry
{
    /// <summary>The ActivitySource for the spans above. ServiceDefaults listens to it.</summary>
    public static readonly ActivitySource Source = new("TicketTide.Modules");
}
