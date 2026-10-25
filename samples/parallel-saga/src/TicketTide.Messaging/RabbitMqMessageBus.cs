using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace TicketTide.Messaging;

/// <summary>
/// Publishes persistent messages with publisher confirms: <see cref="PublishAsync{TMessage}"/> only returns
/// once RabbitMQ has stored the message.
/// </summary>
internal sealed class RabbitMqMessageBus(IConnection connection, ILogger<RabbitMqMessageBus> logger) : IMessageBus, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        var routingKey = Topology.RoutingKeyFor(typeof(TMessage));
        var body = JsonSerializer.SerializeToUtf8Bytes(message, Topology.Json);
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            Type = routingKey,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

        // A channel must not be used by two threads at once.
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            await channel.BasicPublishAsync(Topology.Exchange, routingKey, mandatory: false, properties, body, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        logger.LogInformation("Published {MessageType} {MessageId}", routingKey, properties.MessageId);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await Topology.DeclareExchangesAsync(_channel, cancellationToken);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _lock.Dispose();
    }
}
