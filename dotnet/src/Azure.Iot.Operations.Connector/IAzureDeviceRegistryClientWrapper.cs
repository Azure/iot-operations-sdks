// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    public interface IAzureDeviceRegistryClientWrapper : IAsyncDisposable
    {
        /// <summary>
        /// Executes whenever a asset is created, updated, or deleted.
        /// </summary>
        /// <remarks>
        /// To start receiving these events, use <see cref="ObserveAssets(string, string)"/>.
        /// </remarks>
        event EventHandler<AssetChangedEventArgs>? AssetChanged;

        /// <summary>
        /// Executes whenever a device is created, updated, or deleted.
        /// </summary>
        /// <remarks>
        /// To start receiving these events, use <see cref="ObserveDevices"/>.
        /// </remarks>
        event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        /// <summary>
        /// Start receiving notifications on <see cref="DeviceChanged"/> when any device is created, updated, or deleted.
        /// </summary>
        void ObserveDevices();

        /// <summary>
        /// Stop receiving notifications on <see cref="DeviceChanged"/> when any device is creatd, updated, or deleted.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UnobserveDevicesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Start receiving notifications on <see cref="AssetChanged"/> when any asset associated with the provided endpoint
        /// in the provided device is created, updated, or deleted.
        /// </summary>
        /// <param name="deviceName">The name of the device whose assets will be observed.</param>
        /// <param name="inboundEndpointName">The name of the endpoint within the device whose assets will be observed.</param>
        void ObserveAssets(string deviceName, string inboundEndpointName);

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetChanged"/> when any asset associated with the provided endpoint
        /// in the provided device is created, updated, or deleted.
        /// </summary>
        /// <param name="deviceName">The name of the device whose assets will no longer be observed.</param>
        /// <param name="inboundEndpointName">The name of the endpoint within the device whose assets will no longer be observed.</param>
        Task UnobserveAssetsAsync(string deviceName, string inboundEndpointName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop receiving all asset and device notifications on <see cref="AssetChanged"/> and <see cref="DeviceChanged"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UnobserveAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the credentials to use when connecting to the provided endpoint.
        /// </summary>
        /// <param name="deviceName">The name of the device whose inbound endpoint credentials should be retrieved.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint whose credentials should be retrieved.</param>
        /// <param name="inboundEndpoint">The endpoint whose credentials should be returned.</param>
        /// <returns>The credentials for the endpoint</returns>
        EndpointCredentials GetEndpointCredentials(string deviceName, string inboundEndpointName, InboundEndpointSchemaMapValue inboundEndpoint);

        /// <summary>
        /// List the names of all available assets within the provided endpoint within the provided device.
        /// </summary>
        /// <param name="deviceName">The name of the device to get asset names from.</param>
        /// <param name="inboundEndpointName">The name of the endpoint within the provided device to get asset names from.</param>
        /// <returns>
        /// The collection of asset names associated with the provided endpoint in the provided device.
        /// This collection is empty if the device does not exist (or is unavailable) or if the device has no inbound endpoint with the provided name or if
        /// both the device and inbound endpoint are available, but they have no assets.
        /// </returns>
        IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName);

        /// <summary>
        /// List the names of all available inbound endpoints associated with the provided device name.
        /// </summary>
        /// <param name="deviceName">The device whose inbound endpoint names will be listed.</param>
        /// <returns>
        /// The collection of inbound endpoint names associated with this device. This collection is empty if the device
        /// doesn't exist or isn't available.
        /// </returns>
        IEnumerable<string> GetInboundEndpointNames(string deviceName);

        /// <summary>
        /// List the names of all available devices.
        /// </summary>
        /// <returns>The names of all available devices</returns>
        IEnumerable<string> GetDeviceNames();


        /// <summary>
        /// Retrieves the status of a specific device.
        /// </summary>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
        /// <param name="commandTimeout">Optional timeout for the command.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service.</returns>
        Task<DeviceStatus> GetDeviceStatusAsync(
            string deviceName,
            string inboundEndpointName,
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
        /// <returns>The status returned by the Azure Device Registry service.</returns>
        Task<AssetStatus> GetAssetStatusAsync(
            string deviceName,
            string inboundEndpointName,
            string assetName,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the status of a specific asset.
        /// </summary>
        /// <param name="deviceName">The name of the device the asset belongs to.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint the asset belongs to.</param>
        /// <param name="request">The new status of this asset and its datasets/event groups/streams/management groups.</param>
        /// <param name="commandTimeout">The timeout for this RPC command invocation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service.</returns>
        /// <remarks>
        /// This update behaves like a 'put' in that it will replace all current state for this asset in the Azure
        /// Device Registry service with what is provided. It is generally recommended to use <see cref="GetAssetStatusAsync(string, string, string, TimeSpan?, CancellationToken)"/>
        /// to fetch the current status, patch that status, and then provide it in <paramref name="request"/> to ensure
        /// that no current asset status is lost.
        /// </remarks>
        Task<AssetStatus> UpdateAssetStatusAsync(
            string deviceName,
            string inboundEndpointName,
            UpdateAssetStatusRequest request,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Update the status of a specific device in the Azure Device Registry service.
        /// </summary>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint.</param>
        /// <param name="status">The new status of the device.</param>
        /// <param name="commandTimeout">Optional timeout for the command.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service</returns>
        /// <remarks>
        /// This update call will act as a patch for all endpoint level statuses, but will act as a put for the device-level status.
        /// That means that, for devices with multiple endpoints, you can safely call this method when each endpoint has a status to
        /// report without needing to include the existing status of previously reported endpoints.
        /// </remarks>
        Task<DeviceStatus> UpdateDeviceStatusAsync(
            string deviceName,
            string inboundEndpointName,
            DeviceStatus status,
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
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReportDeviceEndpointRuntimeHealthEvent(string deviceName, string inboundEndpointName, DeviceEndpointRuntimeHealthEventTelemetry telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Report the health of a given asset dataset.
        /// </summary>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the endpoint.</param>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReportDatasetRuntimeHealthEvent(string deviceName, string inboundEndpointName, DatasetRuntimeHealthEventTelemetry telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Report the health of a given asset event.
        /// </summary>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the endpoint.</param>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReportEventRuntimeHealthEvent(string deviceName, string inboundEndpointName, EventRuntimeHealthEventTelemetry telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Report the health of a given asset stream.
        /// </summary>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the endpoint.</param>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReportStreamRuntimeHealthEvent(string deviceName, string inboundEndpointName, StreamRuntimeHealthEventTelemetry telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Report the health of a given management action.
        /// </summary>
        /// <param name="deviceName">The name of the device.</param>
        /// <param name="inboundEndpointName">The name of the endpoint.</param>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReportManagementActionRuntimeHealthEvent(string deviceName, string inboundEndpointName, ManagementActionRuntimeHealthEventTelemetry telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

    }
}
