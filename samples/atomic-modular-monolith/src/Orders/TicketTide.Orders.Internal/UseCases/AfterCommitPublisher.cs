using Mediator;
using Microsoft.Extensions.Logging;

namespace TicketTide.Orders.Internal.UseCases;

/// <summary>
/// Publishes an integration event once a transaction has committed. Whatever the other modules do with it isn't part
/// of the order, so a failure there is logged, and the request still succeeds: the order is already saved.
/// </summary>
public sealed partial class AfterCommitPublisher(IPublisher publisher, ILogger<AfterCommitPublisher> logger)
{
    public async Task PublishAsync<TNotification>(TNotification notification)
        where TNotification : INotification
    {
        try
        {
            await publisher.Publish(notification, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogPublishFailed(logger, ex, typeof(TNotification).Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A handler of {Notification} failed. The order was already committed, so it stands.")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string notification);
}
