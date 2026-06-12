using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Infrastructure.Outbox;

/// <summary>
/// Outbox Pattern: stores domain events in the same DB transaction as the
/// aggregate state change. A background processor publishes them to the
/// message broker, guaranteeing at-least-once delivery.
/// </summary>
[Table("OutboxMessages")]
public sealed class OutboxMessage
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Full assembly-qualified type name of the event.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>JSON-serialised event payload.</summary>
    public string Content { get; init; } = string.Empty;

    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;

    /// <summary>Set when the processor successfully publishes the event.</summary>
    public DateTime? ProcessedOn { get; set; }

    /// <summary>Last error message if publishing failed.</summary>
    public string? Error { get; set; }

    /// <summary>Number of delivery attempts.</summary>
    public int RetryCount { get; set; }

    public string CorrelationId { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
}
