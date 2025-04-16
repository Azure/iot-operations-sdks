// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.Assets
{
    public class AssetCreatedEventArgs : EventArgs
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        public string AssetName { get; set; }

        internal AssetCreatedEventArgs(string deviceName, string inboundEndpointName, string assetName)
        {
            DeviceName = deviceName;
            AssetName = assetName;
            InboundEndpointName = inboundEndpointName;
        }
    }
}
