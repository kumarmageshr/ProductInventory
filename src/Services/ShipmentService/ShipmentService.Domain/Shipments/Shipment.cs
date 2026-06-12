using Shared.Domain.Primitives;

namespace ShipmentService.Domain.Shipments;

/// <summary>
/// Shipment Aggregate Root.
/// </summary>
public sealed class Shipment : AggregateRoot<ShipmentId>
{
    private Shipment() { }

    private Shipment(ShipmentId id, Guid orderId, Guid customerId, string carrier)
    {
        Id = id;
        OrderId = orderId;
        CustomerId = customerId;
        Carrier = carrier;
        Status = ShipmentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Carrier { get; private set; } = string.Empty;
    public string? TrackingNumber { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string? TrackingUrl { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime CreatedAt { get; private init; }

    public static Shipment Create(Guid orderId, Guid customerId, string carrier) =>
        new(ShipmentId.New(), orderId, customerId, carrier);

    public Result Dispatch(string trackingNumber, string trackingUrl)
    {
        if (Status != ShipmentStatus.Pending)
            return Result.Failure(ShipmentErrors.InvalidStatusTransition);

        Status = ShipmentStatus.Shipped;
        TrackingNumber = trackingNumber;
        TrackingUrl = trackingUrl;
        ShippedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ShipmentDispatchedDomainEvent(
            Id.Value, OrderId, trackingNumber, Carrier));
        return Result.Success();
    }

    public Result ConfirmDelivery()
    {
        if (Status != ShipmentStatus.Shipped)
            return Result.Failure(ShipmentErrors.InvalidStatusTransition);

        Status = ShipmentStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;

        RaiseDomainEvent(new DeliveryConfirmedDomainEvent(Id.Value, OrderId));
        return Result.Success();
    }

    public Result MarkFailed(string reason)
    {
        Status = ShipmentStatus.Failed;
        RaiseDomainEvent(new ShipmentFailedDomainEvent(Id.Value, OrderId, reason));
        return Result.Success();
    }
}

public enum ShipmentStatus { Pending, Shipped, Delivered, Failed }

public record ShipmentId(Guid Value)
{
    public static ShipmentId New() => new(Guid.NewGuid());
}

// ── Domain Events ──────────────────────────────────────────────────────────

public sealed record ShipmentDispatchedDomainEvent(
    Guid ShipmentId, Guid OrderId, string TrackingNumber, string Carrier) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record DeliveryConfirmedDomainEvent(
    Guid ShipmentId, Guid OrderId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record ShipmentFailedDomainEvent(
    Guid ShipmentId, Guid OrderId, string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

// ── Errors ─────────────────────────────────────────────────────────────────

public static class ShipmentErrors
{
    public static readonly Error InvalidStatusTransition =
        new("Shipment.InvalidTransition", "Invalid shipment status transition.");
    public static readonly Error NotFound =
        new("Shipment.NotFound", "Shipment not found.");
}
