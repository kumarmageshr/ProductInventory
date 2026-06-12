using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure.Messaging;

/// <summary>
/// Factory Pattern implementation for event bus creation.
/// Reads "Messaging:Provider" from configuration and registers the correct
/// implementation without any code changes — Open/Closed Principle.
/// </summary>
public static class EventBusFactory
{
    public static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Messaging:Provider"]
            ?? throw new InvalidOperationException("Messaging:Provider configuration is missing.");

        return provider switch
        {
            "AzureServiceBus" => services.AddAzureServiceBus(configuration),
            "RabbitMQ"        => services.AddRabbitMq(configuration),
            "Kafka"           => services.AddKafka(configuration),
            _ => throw new InvalidOperationException($"Unsupported messaging provider: {provider}")
        };
    }

    private static IServiceCollection AddAzureServiceBus(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureServiceBusOptions>(
            configuration.GetSection("Messaging:AzureServiceBus"));
        services.AddSingleton<IEventBus, AzureServiceBusEventBus>();
        return services;
    }

    private static IServiceCollection AddRabbitMq(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(
            configuration.GetSection("Messaging:RabbitMQ"));
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        return services;
    }

    private static IServiceCollection AddKafka(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(
            configuration.GetSection("Messaging:Kafka"));
        services.AddSingleton<IEventBus, KafkaEventBus>();
        return services;
    }
}

public sealed class AzureServiceBusOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public int MaxRetryCount { get; init; } = 3;
    public int MaxConcurrentCalls { get; init; } = 10;
    public bool EnableDeadLettering { get; init; } = true;
    public bool EnableDuplicateDetection { get; init; } = true;
    public bool EnableSessions { get; init; } = false;
}

public sealed class RabbitMqOptions
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public int RetryCount { get; init; } = 3;
}

public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string GroupId { get; init; } = "ecommerce-group";
    public int RetryCount { get; init; } = 3;
}
