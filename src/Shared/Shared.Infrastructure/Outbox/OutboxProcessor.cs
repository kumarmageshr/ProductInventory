using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared.Infrastructure.Messaging;

namespace Shared.Infrastructure.Outbox;

/// <summary>
/// Outbox Processor — runs as a hosted background service.
/// Polls the OutboxMessages table and publishes unpublished events to the
/// message bus, then marks them as processed.
/// Guarantees at-least-once delivery with idempotent consumers.
/// </summary>
public sealed class OutboxProcessor<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor<TDbContext>> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;
    private const int MaxRetries = 5;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started for {Context}", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOn == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type)!;
                var payload = JsonConvert.DeserializeObject(message.Content, eventType)!;

                // Invoke PublishAsync<T> dynamically
                var publishMethod = typeof(IEventBus)
                    .GetMethod(nameof(IEventBus.PublishAsync))!
                    .MakeGenericMethod(eventType);

                await (Task)publishMethod.Invoke(eventBus, [payload, ct])!;

                message.ProcessedOn = DateTime.UtcNow;
                message.Error = null;

                _logger.LogInformation("Processed outbox message {MessageId} type {Type}",
                    message.Id, message.Type);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;

                _logger.LogError(ex,
                    "Failed to process outbox message {MessageId} (attempt {Attempt})",
                    message.Id, message.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
