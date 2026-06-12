using Shared.Contracts.Events;

namespace Shared.Infrastructure.Messaging;

/// <summary>
/// Core messaging abstraction — switchable between Azure Service Bus, RabbitMQ, and Kafka.
/// Implement once; swap providers via configuration.
/// </summary>
public interface IEventBus
{
    /// <summary>Publish an integration event to a topic.</summary>
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent;

    /// <summary>Subscribe a handler to a topic/subscription.</summary>
    void Subscribe<T, THandler>()
        where T : IIntegrationEvent
        where THandler : IIntegrationEventHandler<T>;
}

/// <summary>Handler for integration events received from the bus.</summary>
public interface IIntegrationEventHandler<in T>
    where T : IIntegrationEvent
{
    Task HandleAsync(T integrationEvent, CancellationToken cancellationToken = default);
}
