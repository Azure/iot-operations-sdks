// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// EventArgs with context about which device changed and what kind of change happened to it.
    /// </summary>
    public class DeviceChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Specifies if the change in this asset endpoint profile was that it was updated, deleted, or created
        /// </summary>
        public ChangeType ChangeType { get; set; }

        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        public Device? Device { get; set; }

        internal DeviceChangedEventArgs(string deviceName, string inboundEndpointName, ChangeType changeType, Device? device)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
            ChangeType = changeType;
            Device = device;
        }
    }
}
