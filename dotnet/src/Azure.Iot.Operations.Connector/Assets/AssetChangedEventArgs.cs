// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.Assets
{
    public class AssetChangedEventArgs : EventArgs
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        public string AssetName { get; set; }

        public AssetFileMonitorChangeType ChangeType { get; set; }

        internal AssetChangedEventArgs(string deviceName, string inboundEndpointName, string assetName, AssetFileMonitorChangeType changeType)
        {
            DeviceName = deviceName;
            AssetName = assetName;
            InboundEndpointName = inboundEndpointName;
            AssetName = assetName;
            ChangeType = changeType;
        }
    }
}
