// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    public interface IAdrClientWrapper : IAsyncDisposable
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
    }
}
