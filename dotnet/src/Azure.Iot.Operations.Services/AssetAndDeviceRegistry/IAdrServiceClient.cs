using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

/// <summary>
///     Defines methods and events for interacting with the Asset and Device Registry (ADR) service.
///     [CommandTopic("akri/connector/resources/{ex:connectorClientId}/{ex:deviceName}/{ex:inboundEndpointName}/{commandName}")]
///     [TelemetryTopic("akri/connector/resources/telemetry/{ex:connectorClientId}/{ex:deviceName}/{ex:inboundEndpointName}/{telemetryName}")]
/// </summary>
public interface IAdrServiceClient : IAsyncDisposable
{
    Task<NotificationResponse> ObserveDeviceEndpointUpdatesAsync(string deviceName, string endpointName,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<NotificationResponse> UnobserveDeviceEndpointUpdatesAsync(string deviceName, string inboundEndpointName,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<Device> GetDeviceAsync(string deviceName, string endpointName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<Device> UpdateDeviceStatusAsync(string deviceName,
        string endpointName,
        DeviceStatus status,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> ObserveAssetUpdatesAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> UnobserveAssetUpdatesAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<Asset> GetAssetAsync(string deviceName, string inboundEndpointName,
        GetAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<Asset> UpdateAssetStatusAsync(string deviceName,
        string inboundEndpointName,
        UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDetectedAssetResponse> CreateDetectedAssetAsync(string deviceName, string inboundEndpointName,
        CreateDetectedAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDiscoveredAssetEndpointProfileResponse> CreateDiscoveredAssetEndpointProfileAsync(string deviceName, string inboundEndpointName,
        CreateDiscoveredAssetEndpointProfileRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    event Func<string, Device?, Task>? OnReceiveDeviceUpdateEventTelemetry;

    event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry;
}
