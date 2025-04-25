// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    public class DeviceUnavailableEventArgs : EventArgs
    {
        public string DeviceName { get; }

        public string InboundEndpointName { get; }

        internal DeviceUnavailableEventArgs(string deviceName, string inboundEndpointName)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
        }
    }
}
