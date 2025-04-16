// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    public interface IAdrClientWrapper
    {
        event EventHandler<AssetChangedEventArgs>? AssetChanged;

        event EventHandler<DeviceChangedEventArgs>? AssetEndpointProfileChanged;

        void ObserveDevices();

        void ObserveAssets(string deviceName, string inboundEndpointName);

        void UnobserveDevices();

        void UnobserveAssets(string deviceName, string inboundEndpointName);

        Task UnobserveAllAsync(CancellationToken cancellationToken = default);
    }
}
