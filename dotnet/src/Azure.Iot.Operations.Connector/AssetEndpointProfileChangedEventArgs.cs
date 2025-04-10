// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
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

        public string AssetEndpointProfileName { get; set; }

        public AssetEndpointProfile? AssetEndpointProfile { get; set; }

        internal AssetEndpointProfileChangedEventArgs(string assetEndpointProfileName, ChangeType changeType, AssetEndpointProfile? assetEndpointProfile)
        {
            AssetEndpointProfileName = assetEndpointProfileName;
            ChangeType = changeType;
            AssetEndpointProfile = assetEndpointProfile;
        }
    }
}
