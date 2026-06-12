using Microsoft.Extensions.Logging;
using ShipmentService.Domain.Shipments;

namespace ShipmentService.Infrastructure.Carriers;

/// <summary>
/// Adapter Pattern — target interface for external shipping providers.
/// Strategy Pattern — at runtime, the correct carrier is selected per order.
///
/// IShippingProvider
///   └── FedExShippingAdapter
///   └── UPSShippingAdapter
///   └── DHLShippingAdapter
/// </summary>
public interface IShippingProvider
{
    string CarrierName { get; }

    Task<ShippingResult> CreateShipmentAsync(
        CreateShipmentRequest request,
        CancellationToken cancellationToken = default);

    Task<TrackingResult> TrackAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default);
}

public sealed record CreateShipmentRequest(
    Guid OrderId,
    string RecipientName,
    string AddressLine1,
    string City,
    string Country,
    string PostalCode,
    decimal WeightKg);

public sealed record ShippingResult(
    bool IsSuccess,
    string? TrackingNumber,
    string? TrackingUrl,
    string? Error);

public sealed record TrackingResult(
    string TrackingNumber,
    string Status,
    string? Location,
    DateTime? EstimatedDelivery);

// ── FedEx Adapter ──────────────────────────────────────────────────────────

public sealed class FedExShippingAdapter : IShippingProvider
{
    private readonly ILogger<FedExShippingAdapter> _logger;

    public FedExShippingAdapter(ILogger<FedExShippingAdapter> logger) => _logger = logger;

    public string CarrierName => "FedEx";

    public async Task<ShippingResult> CreateShipmentAsync(
        CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("FedEx: Creating shipment for Order {OrderId}", request.OrderId);
        // Production: call FedEx API
        await Task.Delay(80, cancellationToken);

        return new ShippingResult(
            IsSuccess: true,
            TrackingNumber: $"FX{DateTime.UtcNow.Ticks}",
            TrackingUrl: $"https://www.fedex.com/track?trknbr=FX{DateTime.UtcNow.Ticks}",
            Error: null);
    }

    public async Task<TrackingResult> TrackAsync(
        string trackingNumber, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
        return new TrackingResult(trackingNumber, "In Transit", "Memphis Hub", DateTime.UtcNow.AddDays(2));
    }
}

// ── UPS Adapter ────────────────────────────────────────────────────────────

public sealed class UPSShippingAdapter : IShippingProvider
{
    private readonly ILogger<UPSShippingAdapter> _logger;

    public UPSShippingAdapter(ILogger<UPSShippingAdapter> logger) => _logger = logger;

    public string CarrierName => "UPS";

    public async Task<ShippingResult> CreateShipmentAsync(
        CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("UPS: Creating shipment for Order {OrderId}", request.OrderId);
        await Task.Delay(80, cancellationToken);

        return new ShippingResult(
            IsSuccess: true,
            TrackingNumber: $"1Z{Guid.NewGuid():N}"[..18],
            TrackingUrl: $"https://www.ups.com/track?tracknum=1Z{Guid.NewGuid():N}"[..40],
            Error: null);
    }

    public async Task<TrackingResult> TrackAsync(
        string trackingNumber, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
        return new TrackingResult(trackingNumber, "Out for Delivery", "Local Facility", DateTime.UtcNow.AddDays(1));
    }
}

/// <summary>
/// Strategy Pattern + Factory — selects the appropriate carrier.
/// </summary>
public interface IShippingProviderFactory
{
    IShippingProvider Create(string carrierName);
}

public sealed class ShippingProviderFactory : IShippingProviderFactory
{
    private readonly IEnumerable<IShippingProvider> _providers;

    public ShippingProviderFactory(IEnumerable<IShippingProvider> providers) =>
        _providers = providers;

    public IShippingProvider Create(string carrierName) =>
        _providers.FirstOrDefault(p =>
            p.CarrierName.Equals(carrierName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Carrier '{carrierName}' not registered.");
}
