// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Files
{
    public class DeviceFileChangedEventArgs : EventArgs
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        public AssetFileMonitorChangeType ChangeType { get; set; }

        internal DeviceFileChangedEventArgs(string deviceName, string inboundEndpointName, AssetFileMonitorChangeType changeType)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
            ChangeType = changeType;
        }
    }
}
