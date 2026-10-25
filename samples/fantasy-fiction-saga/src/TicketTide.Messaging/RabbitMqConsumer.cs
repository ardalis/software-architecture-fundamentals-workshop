using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TicketTide.Messaging;

/// <summary>
/// Consumes every module's queue. Each delivery is handled in its own DI scope, then acknowledged.
/// If the handler throws, a copy of the message goes to the module's retry queue, waits there for
/// <see cref="Topology.RetryDelay"/> and comes back; after <see cref="Topology.MaxAttempts"/> attempts it goes to
/// the dead-letter queue. A message that cannot be read at all goes to the dead-letter queue at once.
/// </summary>
internal sealed class RabbitMqConsumer(
    IConnection connection,
    IEnumerable<Subscription> subscriptions,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqConsumer> logger) : IHostedService, IAsyncDisposable
{
    // Our own attempt counter, carried on each copy we send to the retry queue.
    private const string AttemptHeader = "x-attempt";

    private readonly List<IChannel> _channels = [];
    private readonly CancellationTokenSource _stopping = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using (var setup = await connection.CreateChannelAsync(cancellationToken: cancellationToken))
        {
            await Topology.DeclareExchangesAsync(setup, cancellationToken);
        }

        foreach (var queue in subscriptions.GroupBy(s => s.Queue))
        {
            var handlers = queue.ToDictionary(s => s.RoutingKey);

            // Publisher confirms: a retry copy is safely stored before we acknowledge the original.
            var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);
            await Topology.DeclareQueueAsync(channel, queue.Key, handlers.Keys, cancellationToken);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, queue.Key, handlers, delivery);
            await channel.BasicConsumeAsync(queue.Key, autoAck: false, consumer, cancellationToken);

            _channels.Add(channel);
            logger.LogInformation("Consuming {Queue} for {MessageTypes}", queue.Key, string.Join(", ", handlers.Keys));
        }
    }

    private async Task HandleAsync(IChannel channel, string queue, Dictionary<string, Subscription> handlers, BasicDeliverEventArgs delivery)
    {
        var ct = _stopping.Token;
        var messageType = delivery.BasicProperties.Type ?? delivery.RoutingKey;
        if (!handlers.TryGetValue(messageType, out var subscription))
        {
            logger.LogError("Queue {Queue} has no handler for {MessageType}; dead-lettering it", queue, messageType);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, ct);
            return;
        }

        // A message we cannot read will never succeed, so retrying it is pointless.
        object message;
        try
        {
            message = JsonSerializer.Deserialize(delivery.Body.Span, subscription.MessageType, Topology.Json)
                ?? throw new JsonException("The message body is null.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "{Queue} could not read {MessageType}; dead-lettering it", queue, messageType);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, ct);
            return;
        }

        var context = new MessageContext(queue, delivery.BasicProperties.MessageId ?? string.Empty, AttemptOf(delivery));
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await subscription.Dispatch(scope.ServiceProvider, message, context, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Don't wait here: the channel delivers one message at a time, so waiting would hold up the whole
            // queue. Send a copy to wait in the retry queue instead, and move on.
            if (context.DeliveryAttempt >= Topology.MaxAttempts)
            {
                logger.LogError(ex, "{Queue} failed to handle {MessageType} {Attempts} times; dead-lettering it",
                    queue, messageType, context.DeliveryAttempt);
                await RepublishAsync(channel, Topology.DeadLetterExchange, queue, delivery, context.DeliveryAttempt, ct);
            }
            else
            {
                var retryQueue = Topology.RetryQueueFor(queue);
                logger.LogWarning(ex, "{Queue} failed to handle {MessageType} (attempt {Attempt}); retrying via {RetryQueue}",
                    queue, messageType, context.DeliveryAttempt, retryQueue);
                await RepublishAsync(channel, string.Empty, retryQueue, delivery, context.DeliveryAttempt + 1, ct);
            }
        }

        // Handled, or safely handed to the retry or dead-letter queue: either way this delivery is done.
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
    }

    /// <summary>
    /// Publishes a persistent copy of <paramref name="delivery"/> (same body, type and message id) with
    /// <paramref name="attempt"/> in its attempt header, and waits for RabbitMQ to confirm it has stored it.
    /// </summary>
    private static async Task RepublishAsync(
        IChannel channel, string exchange, string routingKey, BasicDeliverEventArgs delivery, int attempt, CancellationToken ct)
    {
        var headers = new Dictionary<string, object?>(delivery.BasicProperties.Headers ?? new Dictionary<string, object?>())
        {
            [AttemptHeader] = attempt,
        };
        headers.Remove("x-delivery-count"); // RabbitMQ's count belongs to the original, not to the copy.

        var properties = new BasicProperties(delivery.BasicProperties)
        {
            Headers = headers,
            DeliveryMode = DeliveryModes.Persistent,
        };

        await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, properties, delivery.Body, ct);
    }

    // Our attempt header counts trips through the retry queue. If the process crashed mid-handling, the quorum
    // queue redelivers the same copy and counts that in its x-delivery-count header instead. Take the larger.
    private static int AttemptOf(BasicDeliverEventArgs delivery)
    {
        var headers = delivery.BasicProperties.Headers;
        var attempt = IntHeader(headers, AttemptHeader) ?? 1;
        var redeliveries = IntHeader(headers, "x-delivery-count") ?? 0;
        return Math.Max(attempt, redeliveries + 1);
    }

    private static int? IntHeader(IDictionary<string, object?>? headers, string name) =>
        headers?.TryGetValue(name, out var value) == true && value is not null ? Convert.ToInt32(value) : null;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        foreach (var channel in _channels)
        {
            await channel.CloseAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels)
        {
            await channel.DisposeAsync();
        }

        _stopping.Dispose();
    }
}
