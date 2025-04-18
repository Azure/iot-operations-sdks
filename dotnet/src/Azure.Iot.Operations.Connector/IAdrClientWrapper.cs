// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Assets;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    public interface IAdrClientWrapper
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
        /// Get the credentials to use when connecting to the provided endpoint on the provided device.
        /// </summary>
        /// <param name="deviceName">The name of the device whose credentials will be retrieved.</param>
        /// <param name="inboundEndpointName">The name of the endpoint on the device whose credentials will be retrieved.</param>
        /// <returns>The credentials to use when connecting to the provided endpoint on the provided device.</returns>
        DeviceCredentials GetDeviceCredentials(string deviceName, string inboundEndpointName);

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
    }
}
