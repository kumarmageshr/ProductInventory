using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts.Events;
using System.Text;

namespace Shared.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ implementation of IEventBus.
/// Uses topic exchange with dead-letter support and manual acknowledgement.
/// </summary>
public sealed class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly IModel _channel;
    private readonly IConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private const string ExchangeName = "ecommerce.events";
    private const string DeadLetterExchange = "ecommerce.events.dlx";

    public RabbitMqEventBus(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqEventBus> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare dead-letter exchange
        _channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Topic, durable: true);

        // Declare main topic exchange
        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
    }

    public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
    {
        var routingKey = typeof(T).Name;
        var payload = JsonConvert.SerializeObject(integrationEvent);
        var body = Encoding.UTF8.GetBytes(payload);

        var props = _channel.CreateBasicProperties();
        props.Persistent = true;
        props.MessageId = integrationEvent.EventId.ToString();
        props.CorrelationId = integrationEvent.CorrelationId;
        props.ContentType = "application/json";
        props.Headers = new Dictionary<string, object>
        {
            ["TraceId"] = integrationEvent.TraceId,
            ["EventType"] = typeof(T).Name
        };

        _channel.BasicPublish(ExchangeName, routingKey, props, body);

        _logger.LogInformation("Published {EventType} to RabbitMQ routing key {RoutingKey}",
            typeof(T).Name, routingKey);

        return Task.CompletedTask;
    }

    public void Subscribe<T, THandler>()
        where T : IIntegrationEvent
        where THandler : IIntegrationEventHandler<T>
    {
        var routingKey = typeof(T).Name;
        var queueName = $"{typeof(THandler).Name}.{routingKey}";
        var dlqName = $"{queueName}.dlq";

        // Declare DLQ
        _channel.QueueDeclare(dlqName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(dlqName, DeadLetterExchange, routingKey);

        // Declare queue with dead-letter routing
        var args = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = routingKey,
            ["x-message-ttl"] = 86_400_000, // 24h
            ["x-max-retries"] = _options.RetryCount
        };

        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, args);
        _channel.QueueBind(queueName, ExchangeName, routingKey);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.Span);
            try
            {
                var @event = JsonConvert.DeserializeObject<T>(body)!;
                _logger.LogInformation("Received {EventType}", typeof(T).Name);
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {EventType}, NACKing", typeof(T).Name);
                // Requeue=false → goes to DLQ after max-retries
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queueName, autoAck: false, consumer);
        _logger.LogInformation("Subscribed {Handler} to RabbitMQ queue {Queue}",
            typeof(THandler).Name, queueName);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
