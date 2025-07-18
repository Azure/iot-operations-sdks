// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.Files
{
    public interface IAssetFileMonitor
    {
        /// <summary>
        /// Executes whenever an asset is added to or removed from an existing device after observing asset changes with <see cref="ObserveAssets(string, string)"/>.
        /// </summary>
        event EventHandler<AssetFileChangedEventArgs>? AssetFileChanged;

        /// <summary>
        /// Executes whenever a device file is created or deleted after observing device changes with <see cref="ObserveDevices"/>.
        /// </summary>
        event EventHandler<DeviceFileChangedEventArgs>? DeviceFileChanged;

        /// <summary>
        /// Start observing changes to assets for the given endpoint within the given device.
        /// </summary>
        /// <param name="deviceName">The name of the device to observe asset changes within</param>
        /// <param name="inboundEndpointName">The name of the endpoint within the device to observe asset changes within</param>
        void ObserveAssets(string deviceName, string inboundEndpointName);

        /// <summary>
        /// Stop observing changes to assets for the given endpoint within the given device.
        /// </summary>
        /// <param name="deviceName">The name of the device to stop observing asset changes within</param>
        /// <param name="inboundEndpointName">The name of the endpoint within the device to stop observing asset changes within</param>
        void UnobserveAssets(string deviceName, string inboundEndpointName);

        /// <summary>
        /// Start observing changes to all devices.
        /// </summary>
        void ObserveDevices();

        /// <summary>
        /// Stop observing changes to all devices.
        /// </summary>
        void UnobserveDevices();

        /// <summary>
        /// List the names of all assets within the provided endpoint within the provided device.
        /// </summary>
        /// <param name="deviceName">The name of the device to get asset names from.</param>
        /// <param name="inboundEndpointName">The name of the endpoint within the provided device to get asset names from.</param>
        /// <returns>
        /// The collection of asset names associated with the provided endpoint in the provided device.
        /// This collection is empty if the device does not exist or if the device has no inbound endpoint with the provided name or if
        /// both the device and inbound endpoint exist, but they have no assets.
        /// </returns>
        IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName);

        /// <summary>
        /// List the names of inbound endpoints associated with the provided device name.
        /// </summary>
        /// <param name="deviceName">The device whose inbound endpoint names will be listed.</param>
        /// <returns>
        /// The collection of inbound endpoint names associated with this device. This collection is empty if the device
        /// doesn't exist.
        /// </returns>
        IEnumerable<string> GetInboundEndpointNames(string deviceName);

        /// <summary>
        /// List the names of all devices.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetDeviceNames();

        /// <summary>
        /// Get the file-mounted credentials for the provided inbound endpoint.
        /// </summary>
        /// <param name="deviceName">The name of the device whose inbound endpoint credentials should be retrieved.</param>
        /// <param name="inboundEndpointName">The name of the inbound endpoint whose credentials should be retrieved.</param>
        /// <param name="inboundEndpoint">The endpoint whose credentials should be returned.</param>
        /// <returns>The credentials for the endpoint</returns>
        EndpointCredentials GetEndpointCredentials(string deviceName, string inboundEndpointName, InboundEndpointSchemaMapValue inboundEndpoint);

        /// <summary>
        /// Stop all observation of assets and/or devices.
        /// </summary>
        void UnobserveAll();
    }
}
