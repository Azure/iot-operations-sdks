// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Azure.Iot.Operations.Services.Assets
{
    /// <summary>
    /// EventArgs with context about which AssetEndpointProfile changed and what kind of change happened to it.
    /// </summary>
    public class AssetEndpointProfileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Specifies if the change in this asset endpoint profile was that it was updated, deleted, or created
        /// </summary>
        public ChangeType ChangeType { get; set; }

        /// <summary>
        /// The 
        /// </summary>
        public AssetEndpointProfile? AssetEndpointProfile { get; set; }

        internal AssetEndpointProfileChangedEventArgs(ChangeType changeType, AssetEndpointProfile? assetEndpointProfile)
        {
            ChangeType = changeType;
            AssetEndpointProfile = assetEndpointProfile;
        }
    }
}
