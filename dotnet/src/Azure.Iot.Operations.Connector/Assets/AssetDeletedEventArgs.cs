// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Assets
{
    /// <summary>
    /// EventArgs with context about which Asset changed and what kind of change happened to it.
    /// </summary>
    public class AssetDeletedEventArgs : EventArgs
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        /// <summary>
        /// The name of the asset that changed. This value is provided even if the asset was deleted.
        /// </summary>
        public string AssetName { get; set; }

        internal AssetDeletedEventArgs(string deviceName, string inboundEndpointName, string assetName)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
            AssetName = assetName;
        }
    }
}
