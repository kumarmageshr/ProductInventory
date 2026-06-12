using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;
using Shared.Domain.Primitives;
using Shared.Infrastructure.Outbox;

namespace Shared.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that converts domain events into outbox
/// messages within the same database transaction. This guarantees
/// transactional outbox semantics.
/// </summary>
public sealed class DomainEventToOutboxInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // Collect all domain events from aggregates in the change tracker
        var outboxMessages = context.ChangeTracker
            .Entries<Entity<Guid>>()
            .SelectMany(e =>
            {
                var events = e.Entity.DomainEvents.ToList();
                e.Entity.ClearDomainEvents();
                return events;
            })
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = domainEvent.GetType().AssemblyQualifiedName!,
                Content = JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                }),
                OccurredOn = domainEvent.OccurredOn,
                CorrelationId = string.Empty, // Enriched by middleware
                TraceId = string.Empty
            })
            .ToList();

        if (outboxMessages.Count > 0)
            context.Set<OutboxMessage>().AddRange(outboxMessages);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
