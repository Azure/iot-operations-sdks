// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Azure.Iot.Operations.Services.Assets
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

        internal AssetChangedEventArgs(string assetName, ChangeType changeType, Asset? asset)
        {
            AssetName = assetName;
            ChangeType = changeType;
            Asset = asset;
        }
    }
}
