// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Assets;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    public interface IAdrClientWrapper
    {
        event EventHandler<AssetChangedEventArgs>? AssetChanged;

        event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        void ObserveDevices();

        void ObserveAssets(string deviceName, string inboundEndpointName);

        Task UnobserveDevicesAsync(CancellationToken cancellationToken = default);

        Task UnobserveAssetsAsync(string deviceName, string inboundEndpointName, CancellationToken cancellationToken = default);

        Task UnobserveAllAsync(CancellationToken cancellationToken = default);

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
