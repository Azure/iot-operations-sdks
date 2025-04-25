using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

/// <summary>
/// Defines methods and events for interacting with the Asset and Device Registry (ADR) service.
/// </summary>
public interface IAdrServiceClient : IAsyncDisposable
{
    /// <summary>
    /// Observes updates for a specific device endpoint.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the notification response.</returns>
    Task<NotificationResponse> ObserveDeviceEndpointUpdatesAsync(
        string deviceName,
        string inboundEndpointName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops observing updates for a specific device endpoint.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the notification response.</returns>
    Task<NotificationResponse> UnobserveDeviceEndpointUpdatesAsync(
        string deviceName,
        string inboundEndpointName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details of a specific device.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the device details.</returns>
    Task<Device> GetDeviceAsync(
        string deviceName,
        string inboundEndpointName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a specific device.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="status">The new status of the device.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the updated device details.</returns>
    Task<Device> UpdateDeviceStatusAsync(
        string deviceName,
        string inboundEndpointName,
        DeviceStatus status,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Observes updates for a specific asset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="assetName">The name of the asset.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the notification response.</returns>
    Task<NotificationResponse> ObserveAssetUpdatesAsync(
        string deviceName,
        string inboundEndpointName,
        string assetName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops observing updates for a specific asset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="assetName">The name of the asset.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the notification response.</returns>
    Task<NotificationResponse> UnobserveAssetUpdatesAsync(
        string deviceName,
        string inboundEndpointName,
        string assetName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details of a specific asset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="request">The request containing asset retrieval parameters.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the asset details.</returns>
    Task<Asset> GetAssetAsync(
        string deviceName,
        string inboundEndpointName,
        GetAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a specific asset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="request">The request containing asset status update parameters.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the updated asset details.</returns>
    Task<Asset> UpdateAssetStatusAsync(
        string deviceName,
        string inboundEndpointName,
        UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a detected asset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="request">The request containing detected asset creation parameters.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the response for the created detected asset.</returns>
    Task<CreateDetectedAssetResponse> CreateDetectedAssetAsync(
        string deviceName,
        string inboundEndpointName,
        CreateDetectedAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a discovered asset endpoint profile.
    /// </summary>
    /// <param name="request">The request containing discovered asset endpoint profile creation parameters.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the response for the created discovered asset endpoint profile.</returns>
    Task<CreateDiscoveredAssetEndpointProfileResponse> CreateDiscoveredAssetEndpointProfileAsync(
        CreateDiscoveredAssetEndpointProfileRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when a device update telemetry event is received.
    /// NOTE: This event starts triggering after the call to ObserveDeviceEndpointUpdatesAsync.
    /// </summary>
    event Func<string, Device, Task>? OnReceiveDeviceUpdateEventTelemetry;

    /// <summary>
    /// Event triggered when an asset update telemetry event is received.
    /// NOTE: This event starts triggering after the call to ObserveAssetUpdatesAsync.
    /// </summary>
    event Func<string, Asset, Task>? OnReceiveAssetUpdateEventTelemetry;
}
