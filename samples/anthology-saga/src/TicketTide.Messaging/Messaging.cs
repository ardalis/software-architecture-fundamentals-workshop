using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TicketTide.Messaging;

/// <summary>Publishes a message to RabbitMQ. The routing key is the message type's name.</summary>
public interface IMessageBus
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class;
}

/// <summary>
/// Handles one message type for one module. Throwing an exception means "not done": the message waits
/// a second in the module's retry queue and is delivered again, up to 20 attempts, then it is dead-lettered.
/// </summary>
public interface IMessageHandler<in TMessage>
    where TMessage : class
{
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken);
}

/// <param name="DeliveryAttempt">1 for the first attempt, 2 for the first retry, and so on.</param>
public sealed record MessageContext(string Queue, string MessageId, int DeliveryAttempt);

public static class MessagingExtensions
{
    /// <summary>Adds the RabbitMQ connection (from the Aspire "rabbitmq" resource), the bus, and the consumer.</summary>
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        builder.AddRabbitMQClient("rabbitmq");
        builder.Services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
        builder.Services.AddHostedService<RabbitMqConsumer>();
        return builder;
    }

    /// <summary>
    /// Subscribes <paramref name="queue"/> (one durable queue per module) to <typeparamref name="TMessage"/>,
    /// and handles each delivery with <typeparamref name="THandler"/> in its own DI scope.
    /// </summary>
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services, string queue)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new Subscription(
            queue,
            typeof(TMessage),
            (provider, message, context, ct) =>
                provider.GetRequiredService<THandler>().HandleAsync((TMessage)message, context, ct)));
        return services;
    }
}

/// <summary>One message type handled on one queue, and how to call its handler.</summary>
internal sealed record Subscription(
    string Queue,
    Type MessageType,
    Func<IServiceProvider, object, MessageContext, CancellationToken, Task> Dispatch)
{
    public string RoutingKey => Topology.RoutingKeyFor(MessageType);
}
