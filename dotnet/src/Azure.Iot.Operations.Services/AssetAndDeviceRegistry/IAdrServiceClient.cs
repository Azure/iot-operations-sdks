using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

/// <summary>
/// Defines methods and events for interacting with the Asset and Device Registry (ADR) service.
/// [CommandTopic("akri/connector/resources/{ex:connectorClientId}/{ex:deviceName}/{ex:inboundEndpointName}/{commandName}")]
/// [TelemetryTopic("akri/connector/resources/telemetry/{ex:connectorClientId}/{ex:deviceName}/{ex:inboundEndpointName}/{telemetryName}")]
/// </summary>
public interface IAdrServiceClient : IAsyncDisposable
{
    Task<NotificationResponse> ObserveDeviceUpdatesAsync(string deviceName, string inboundEndpointName,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<NotificationResponse> UnobserveDeviceUpdatesAsync(string deviceName, string inboundEndpointName,
        TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<Device> GetDeviceAsync(string deviceName, TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<Device> UpdateDeviceStatusAsync(string deviceName,
        DeviceStatus status,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> ObserveAssetUpdatesAsync(string aepName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<NotificationResponse> UnobserveAssetUpdatesAsync(string aepName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<Asset> GetAssetAsync(string aepName,
        GetAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<Asset> UpdateAssetStatusAsync(string aepName,
        UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDetectedAssetResponse> CreateDetectedAssetAsync(string aepName,
        CreateDetectedAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    Task<CreateDiscoveredAssetEndpointProfileResponse> CreateDiscoveredAssetEndpointProfileAsync(string aepName,
        CreateDiscoveredAssetEndpointProfileRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    event Func<string, Device?, Task>? OnReceiveDeviceUpdateEventTelemetry;

    event Func<string, Asset?, Task>? OnReceiveAssetUpdateEventTelemetry;
}
