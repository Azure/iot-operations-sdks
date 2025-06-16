// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    public class DeviceAvailableEventArgs : EventArgs
    {
        public Device Device { get; }

        public string InboundEndpointName { get; }

        internal DeviceAvailableEventArgs(Device device, string inboundEndpointName)
        {
            Device = device;
            InboundEndpointName = inboundEndpointName;
        }
    }
}
