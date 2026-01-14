using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

/// <summary>
/// Defines methods and events for interacting with the Asset and Device Registry (ADR) service.
/// </summary>
public interface IAzureDeviceRegistryClient : IAsyncDisposable
{
    /// <summary>
    /// Observe or unobserve updates for a specific device endpoint.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the notification response.</returns>
    Task<SetNotificationPreferenceForDeviceUpdatesResponsePayload> SetNotificationPreferenceForDeviceUpdatesAsync(
        string deviceName,
        string inboundEndpointName,
        NotificationPreference notificationPreference,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Observe or unobserve updates for a specific asset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="assetName">The name of the asset.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the notification response.</returns>
    Task<SetNotificationPreferenceForAssetUpdatesResponsePayload> SetNotificationPreferenceForAssetUpdatesAsync(
        string deviceName,
        string inboundEndpointName,
        string assetName,
        NotificationPreference notificationPreference,
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
    Task<DeviceStatus> UpdateDeviceStatusAsync(
        string deviceName,
        string inboundEndpointName,
        DeviceStatus status,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the status of a specific device.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the device status.</returns>
    Task<DeviceStatus> GetDeviceStatusAsync(
        string deviceName,
        string inboundEndpointName,
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
        string assetName,
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
    Task<AssetStatus> UpdateAssetStatusAsync(
        string deviceName,
        string inboundEndpointName,
        UpdateAssetStatusRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the status of a specific asset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="assetName">The name of the asset.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the asset status.</returns>
    Task<AssetStatus> GetAssetStatusAsync(string deviceName,
        string inboundEndpointName,
        string assetName,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a discovered asset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
    /// <param name="request">The request containing discovered asset creation parameters.</param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the response for the created discovered asset.</returns>
    Task<CreateOrUpdateDiscoveredAssetResponsePayload> CreateOrUpdateDiscoveredAssetAsync(string deviceName,
        string inboundEndpointName,
        CreateOrUpdateDiscoveredAssetRequest request,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a discovered device.
    /// </summary>
    /// <param name="request">The request containing discovered device endpoint profile creation parameters.</param>
    /// <param name="inboundEndpointType"></param>
    /// <param name="commandTimeout">Optional timeout for the command.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the response for the created discovered device endpoint profile.</returns>
    Task<CreateOrUpdateDiscoveredDeviceResponsePayload> CreateOrUpdateDiscoveredDeviceAsync(
        CreateOrUpdateDiscoveredDeviceRequestSchema request,
        string inboundEndpointType,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Report the health of a given device endpoint.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the endpoint.</param>
    /// <param name="runtimeHealth">The health status to report.</param>
    /// <param name="qos">The MQTT quality of service to send this report with.</param>
    /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportDeviceEndpointRuntimeHealthAsync(
        string deviceName,
        string inboundEndpointName,
        DeviceEndpointRuntimeHealthEventTelemetry runtimeHealth,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
        TimeSpan? telemetryTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Report the health of a given asset dataset.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the endpoint.</param>
    /// <param name="runtimeHealth">The health status to report.</param>
    /// <param name="qos">The MQTT quality of service to send this report with.</param>
    /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportDatasetRuntimeHealthAsync(
        string deviceName,
        string inboundEndpointName,
        DatasetRuntimeHealthEventTelemetry runtimeHealth,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
        TimeSpan? telemetryTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Report the health of a given asset event.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the endpoint.</param>
    /// <param name="runtimeHealth">The health status to report.</param>
    /// <param name="qos">The MQTT quality of service to send this report with.</param>
    /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportEventRuntimeHealthAsync(
        string deviceName,
        string inboundEndpointName,
        EventRuntimeHealthEventTelemetry runtimeHealth,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
        TimeSpan? telemetryTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Report the health of a given asset stream.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the endpoint.</param>
    /// <param name="runtimeHealth">The health status to report.</param>
    /// <param name="qos">The MQTT quality of service to send this report with.</param>
    /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportStreamRuntimeHealthAsync(
        string deviceName,
        string inboundEndpointName,
        StreamRuntimeHealthEventTelemetry runtimeHealth,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
        TimeSpan? telemetryTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Report the health of a given management action.
    /// </summary>
    /// <param name="deviceName">The name of the device.</param>
    /// <param name="inboundEndpointName">The name of the endpoint.</param>
    /// <param name="runtimeHealth">The health status to report.</param>
    /// <param name="qos">The MQTT quality of service to send this report with.</param>
    /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportManagementActionRuntimeHealthAsync(
        string deviceName,
        string inboundEndpointName,
        ManagementActionRuntimeHealthEventTelemetry runtimeHealth,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
        TimeSpan? telemetryTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when a device update telemetry event is received.
    /// NOTE: This event starts triggering after the call to <see cref="SetNotificationPreferenceForDeviceUpdatesAsync(string, string, NotificationPreference, TimeSpan?, CancellationToken)"/>.
    /// </summary>
    event Func<string, string, Device, Task>? OnReceiveDeviceUpdateEventTelemetry;

    /// <summary>
    /// Event triggered when an asset update telemetry event is received.
    /// NOTE: This event starts triggering after the call to <see cref="SetNotificationPreferenceForAssetUpdatesAsync(string, string, string, NotificationPreference, TimeSpan?, CancellationToken)"/>.
    /// </summary>
    event Func<string, Asset, Task>? OnReceiveAssetUpdateEventTelemetry;
}
