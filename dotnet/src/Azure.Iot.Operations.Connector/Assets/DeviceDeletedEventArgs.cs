// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Assets
{
    public class DeviceDeletedEventArgs : EventArgs
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        internal DeviceDeletedEventArgs(string deviceName, string inboundEndpointName)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
        }
    }
}
