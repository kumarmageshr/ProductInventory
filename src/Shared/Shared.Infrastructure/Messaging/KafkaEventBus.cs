using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Shared.Contracts.Events;

namespace Shared.Infrastructure.Messaging;

/// <summary>
/// Apache Kafka implementation of IEventBus.
/// Uses exactly-once semantics via idempotent producer.
/// </summary>
public sealed class KafkaEventBus : IEventBus, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaEventBus> _logger;

    public KafkaEventBus(
        IOptions<KafkaOptions> options,
        ILogger<KafkaEventBus> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            MaxInFlight = 5,
            MessageSendMaxRetries = _options.RetryCount,
            RetryBackoffMs = 1000
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
    {
        var topicName = typeof(T).Name.ToLowerInvariant();
        var payload = JsonConvert.SerializeObject(integrationEvent);

        var message = new Message<string, string>
        {
            Key = integrationEvent.EventId.ToString(),
            Value = payload,
            Headers = new Headers
            {
                { "CorrelationId", System.Text.Encoding.UTF8.GetBytes(integrationEvent.CorrelationId) },
                { "TraceId", System.Text.Encoding.UTF8.GetBytes(integrationEvent.TraceId) },
                { "EventType", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name) }
            }
        };

        var result = await _producer.ProduceAsync(topicName, message, cancellationToken);

        _logger.LogInformation(
            "Published {EventType} to Kafka topic {Topic} partition {Partition} offset {Offset}",
            typeof(T).Name, topicName, result.Partition, result.Offset);
    }

    public void Subscribe<T, THandler>()
        where T : IIntegrationEvent
        where THandler : IIntegrationEventHandler<T>
    {
        // Subscription is handled by background consumer services per service.
        _logger.LogInformation(
            "Registered subscription: {Handler} handles {Event}",
            typeof(THandler).Name, typeof(T).Name);
    }

    public void Dispose() => _producer.Dispose();
}
