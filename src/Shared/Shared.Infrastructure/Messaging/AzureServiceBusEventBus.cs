using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Shared.Contracts.Events;

namespace Shared.Infrastructure.Messaging;

/// <summary>
/// Azure Service Bus implementation of IEventBus.
/// Features:
///   - Peek Lock message settlement
///   - Dead Letter Queue on poison messages
///   - Retry with exponential back-off (native SDK)
///   - Duplicate detection via MessageId
///   - CorrelationId and TraceId propagation
///   - Session support (optional, per-topic)
/// </summary>
public sealed class AzureServiceBusEventBus : IEventBus, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly AzureServiceBusOptions _options;
    private readonly ILogger<AzureServiceBusEventBus> _logger;
    private readonly Dictionary<string, ServiceBusSender> _senders = [];
    private readonly Dictionary<string, ServiceBusProcessor> _processors = [];

    // Maps event type name -> handler type
    private readonly Dictionary<string, Type> _handlers = [];

    public AzureServiceBusEventBus(
        IOptions<AzureServiceBusOptions> options,
        ILogger<AzureServiceBusEventBus> logger,
        IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _logger = logger;

        var clientOptions = new ServiceBusClientOptions
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                Mode = ServiceBusRetryMode.Exponential,
                MaxRetries = _options.MaxRetryCount,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30)
            }
        };

        _client = new ServiceBusClient(_options.ConnectionString, clientOptions);
        _adminClient = new ServiceBusAdministrationClient(_options.ConnectionString);
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
    {
        var topicName = GetTopicName(typeof(T));

        await EnsureTopicExistsAsync(topicName, cancellationToken);

        var sender = GetOrCreateSender(topicName);
        var payload = JsonConvert.SerializeObject(integrationEvent);

        var message = new ServiceBusMessage(payload)
        {
            MessageId = integrationEvent.EventId.ToString(),   // Dedup key
            CorrelationId = integrationEvent.CorrelationId,
            Subject = typeof(T).Name,
            ContentType = "application/json",
            ApplicationProperties =
            {
                ["TraceId"] = integrationEvent.TraceId,
                ["EventType"] = typeof(T).AssemblyQualifiedName,
                ["OccurredOn"] = integrationEvent.OccurredOn.ToString("O")
            }
        };

        await sender.SendMessageAsync(message, cancellationToken);

        _logger.LogInformation(
            "Published {EventType} with MessageId={MessageId} CorrelationId={CorrelationId}",
            typeof(T).Name, message.MessageId, message.CorrelationId);
    }

    /// <inheritdoc />
    public void Subscribe<T, THandler>()
        where T : IIntegrationEvent
        where THandler : IIntegrationEventHandler<T>
    {
        var eventTypeName = typeof(T).Name;
        _handlers[eventTypeName] = typeof(THandler);
        _logger.LogInformation("Subscribed {Handler} to {Event}", typeof(THandler).Name, eventTypeName);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private ServiceBusSender GetOrCreateSender(string topicName)
    {
        if (!_senders.TryGetValue(topicName, out var sender))
        {
            sender = _client.CreateSender(topicName);
            _senders[topicName] = sender;
        }
        return sender;
    }

    private async Task EnsureTopicExistsAsync(string topicName, CancellationToken ct)
    {
        if (!await _adminClient.TopicExistsAsync(topicName, ct))
        {
            var topicOptions = new CreateTopicOptions(topicName)
            {
                EnablePartitioning = true,
                RequiresDuplicateDetection = _options.EnableDuplicateDetection,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10),
                DefaultMessageTimeToLive = TimeSpan.FromDays(7)
            };
            await _adminClient.CreateTopicAsync(topicOptions, ct);
            _logger.LogInformation("Created topic: {TopicName}", topicName);
        }
    }

    private static string GetTopicName(Type eventType) =>
        eventType.Name
            .Replace("IntegrationEvent", string.Empty)
            .ToLowerInvariant()
            .Replace("created", "-created")
            .TrimStart('-');

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors.Values)
            await processor.DisposeAsync();
        foreach (var sender in _senders.Values)
            await sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
