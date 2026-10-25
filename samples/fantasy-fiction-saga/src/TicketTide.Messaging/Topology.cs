using System.Text.Json;
using RabbitMQ.Client;

namespace TicketTide.Messaging;

/// <summary>
/// Every message goes to one topic exchange. Each module has its own durable queue, bound to the
/// message types it handles, plus a retry queue where failed messages wait a moment before they go back.
/// Messages that fail too often go to a dead-letter queue.
/// </summary>
internal static class Topology
{
    public const string Exchange = "tickettide";
    public const string DeadLetterExchange = "tickettide.dead-letter";
    public const string DeadLetterQueue = "tickettide.dead-letter";

    /// <summary>A message whose handler keeps failing is tried this many times, then dead-lettered.</summary>
    public const int MaxAttempts = 20;

    /// <summary>How long a failed message waits in its retry queue before it is delivered again.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string RoutingKeyFor(Type messageType) => messageType.Name;

    public static string RetryQueueFor(string queue) => $"{queue}.retry";

    public static async Task DeclareExchangesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, routingKey: string.Empty, cancellationToken: cancellationToken);
    }

    public static async Task DeclareQueueAsync(IChannel channel, string queue, IEnumerable<string> routingKeys, CancellationToken cancellationToken)
    {
        // Quorum queues are durable and replicated. The consumer handles retries itself (see below), so the
        // delivery limit is only a safety net: a message that keeps crashing the process mid-handling (and so
        // is never acknowledged) is dead-lettered after this many deliveries.
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = MaxAttempts,
                ["x-dead-letter-exchange"] = DeadLetterExchange,
            },
            cancellationToken: cancellationToken);

        foreach (var routingKey in routingKeys)
        {
            await channel.QueueBindAsync(queue, Exchange, routingKey, cancellationToken: cancellationToken);
        }

        // The retry queue has no consumer. A failed message waits here for RetryDelay, then expires and is
        // dead-lettered through the default exchange ("") straight back to the module's queue. Waiting here,
        // instead of inside the handler, keeps the module's queue moving while the message waits.
        await channel.QueueDeclareAsync(RetryQueueFor(queue), durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-message-ttl"] = (int)RetryDelay.TotalMilliseconds,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = queue,
            },
            cancellationToken: cancellationToken);
    }
}
