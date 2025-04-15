// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.Assets
{
    /// <summary>
    /// EventArgs with context about which Asset changed and what kind of change happened to it.
    /// </summary>
    public class AssetCreatedEventArgs : EventArgs
    {
        public string AssetEndpointProfileName { get; set; }

        public string AssetName { get; set; }

        internal AssetCreatedEventArgs(string assetEndpointProfileName, string assetName)
        {
            AssetEndpointProfileName = assetEndpointProfileName;
            AssetName = assetName;
        }
    }
}
