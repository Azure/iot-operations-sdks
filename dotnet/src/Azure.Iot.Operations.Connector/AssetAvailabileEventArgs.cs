// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// The event args for when an asset becomes available to sample.
    /// </summary>
    public class AssetAvailabileEventArgs : EventArgs
    {
        /// <summary>
        /// The name of the asset that is now available to sample.
        /// </summary>
        public string AssetName { get; }

        /// <summary>
        /// The asset that is now available to sample.
        /// </summary>
        public Asset Asset { get; }

        internal AssetAvailabileEventArgs(string assetName, Asset asset)
        {
            AssetName = assetName;
            Asset = asset;
        }
    }
}
