namespace Shared.Contracts.Events;

/// <summary>
/// Marker interface for integration events published across service boundaries.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
    string CorrelationId { get; }
    string TraceId { get; }
}

/// <summary>
/// Base record for all integration events.
/// </summary>
public abstract record IntegrationEvent(
    Guid EventId,
    DateTime OccurredOn,
    string CorrelationId,
    string TraceId) : IIntegrationEvent
{
    protected IntegrationEvent(string correlationId, string traceId)
        : this(Guid.NewGuid(), DateTime.UtcNow, correlationId, traceId) { }
}
