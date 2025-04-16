// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets.FileMonitor;

namespace Azure.Iot.Operations.Services.Assets
{
    public interface IAssetFileMonitor
    {
        event EventHandler<AssetCreatedEventArgs>? AssetCreated;

        event EventHandler<AssetDeletedEventArgs>? AssetDeleted;

        event EventHandler<DeviceCreatedEventArgs>? DeviceCreated;

        event EventHandler<DeviceDeletedEventArgs>? DeviceDeleted;

        void ObserveAssets(string deviceName, string inboundEndpointName);

        void UnobserveAssets(string deviceName, string inboundEndpointName);

        void ObserveDevices();

        void UnobserveDevices();

        IEnumerable<string>? GetAssetNames(string deviceName, string inboundEndpointName);

        IEnumerable<string>? GetDeviceNames();

        void UnobserveAll();
    }
}
