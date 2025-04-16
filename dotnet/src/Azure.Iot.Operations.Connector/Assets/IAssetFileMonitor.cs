// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Assets
{
    public interface IAssetFileMonitor
    {
        event EventHandler<AssetCreatedEventArgs>? AssetFileCreated;

        event EventHandler<AssetDeletedEventArgs>? AssetFileDeleted;

        event EventHandler<DeviceCreatedEventArgs>? DeviceFileCreated;

        event EventHandler<DeviceDeletedEventArgs>? DeviceFileDeleted;

        void ObserveAssets(string deviceName, string inboundEndpointName);

        void UnobserveAssets(string deviceName, string inboundEndpointName);

        void ObserveDevices();

        void UnobserveDevices();

        IEnumerable<string>? GetAssetNames(string deviceName, string inboundEndpointName);

        IEnumerable<string>? GetDeviceNames();

        DeviceCredentials GetDeviceCredentials(string deviceName, string inboundEndpointName);

        void UnobserveAll();
    }
}
