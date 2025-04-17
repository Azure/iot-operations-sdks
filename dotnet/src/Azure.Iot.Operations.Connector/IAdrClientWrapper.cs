// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Connector.Assets;

namespace Azure.Iot.Operations.Connector
{
    public interface IAdrClientWrapper
    {
        event EventHandler<AssetChangedEventArgs>? AssetChanged;

        event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        void ObserveDevices();

        void ObserveAssets(string deviceName, string inboundEndpointName);

        void UnobserveDevices();

        void UnobserveAssets(string deviceName, string inboundEndpointName);

        Task UnobserveAllAsync(CancellationToken cancellationToken = default);

        DeviceCredentials GetDeviceCredentials(string deviceName, string inboundEndpointName);
    }
}
