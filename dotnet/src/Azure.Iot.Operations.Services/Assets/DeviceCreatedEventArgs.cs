// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets
{
    public class DeviceCreatedEventArgs : EventArgs
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        internal DeviceCreatedEventArgs(string deviceName, string inboundEndpointName)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
        }
    }
}
