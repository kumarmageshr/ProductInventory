using MediatR;

namespace Shared.Domain.Primitives;

/// <summary>
/// Marker interface for domain events.
/// Implements INotification so MediatR can dispatch them.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}
