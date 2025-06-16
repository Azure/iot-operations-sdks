// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// EventArgs with context about which Asset changed and what kind of change happened to it.
    /// </summary>
    public class AssetChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Specifies if the change in this asset was that it was updated, deleted, or created
        /// </summary>
        public ChangeType ChangeType { get; set; }

        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        /// <summary>
        /// The name of the asset that changed. This value is provided even if the asset was deleted.
        /// </summary>
        public string AssetName { get; set; }

        /// <summary>
        /// The new value of the asset.
        /// </summary>
        /// <remarks>
        /// This value is null if the asset was deleted.
        /// </remarks>
        public Asset? Asset { get; set; }

        internal AssetChangedEventArgs(string deviceName, string inboundEndpointName, string assetName, ChangeType changeType, Asset? asset)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
            AssetName = assetName;
            ChangeType = changeType;
            Asset = asset;
        }
    }
}
