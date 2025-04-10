// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets
{
    /// <summary>
    /// EventArgs with context about which Asset changed and what kind of change happened to it.
    /// </summary>
    public class AssetDeletedEventArgs : EventArgs
    {
        public string AssetEndpointProfileName { get; set; }

        /// <summary>
        /// The name of the asset that changed. This value is provided even if the asset was deleted.
        /// </summary>
        public string AssetName { get; set; }

        internal AssetDeletedEventArgs(string assetEndpointProfileName, string assetName)
        {
            AssetEndpointProfileName = assetEndpointProfileName;
            AssetName = assetName;
        }
    }
}
