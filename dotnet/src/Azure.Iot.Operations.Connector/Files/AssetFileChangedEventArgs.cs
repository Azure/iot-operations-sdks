// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.Files
{
    public class AssetFileChangedEventArgs : EventArgs
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        public string AssetName { get; set; }

        public FileChangeType ChangeType { get; set; }

        internal AssetFileChangedEventArgs(string deviceName, string inboundEndpointName, string assetName, FileChangeType changeType)
        {
            DeviceName = deviceName;
            AssetName = assetName;
            InboundEndpointName = inboundEndpointName;
            AssetName = assetName;
            ChangeType = changeType;
        }
    }
}
