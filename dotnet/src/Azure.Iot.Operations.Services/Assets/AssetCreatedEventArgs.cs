// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.Assets
{
    /// <summary>
    /// EventArgs with context about which Asset changed and what kind of change happened to it.
    /// </summary>
    public class AssetCreatedEventArgs : EventArgs
    {
        public string AssetEndpointProfileName { get; set; }

        public string AssetName { get; set; }

        public Asset Asset { get; set; }

        internal AssetCreatedEventArgs(string assetEndpointProfileName, string assetName, Asset asset)
        {
            AssetEndpointProfileName = assetEndpointProfileName;
            AssetName = assetName;
            Asset = asset;
        }
    }
}
