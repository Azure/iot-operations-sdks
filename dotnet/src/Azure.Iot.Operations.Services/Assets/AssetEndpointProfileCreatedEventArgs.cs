// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.Assets
{
    public class AssetEndpointProfileCreatedEventArgs : EventArgs
    {
        public string AssetEndpointProfileName { get; set; }

        public AssetEndpointProfile AssetEndpointProfile { get; set; }

        internal AssetEndpointProfileCreatedEventArgs(string name, AssetEndpointProfile assetEndpointProfile)
        {
            AssetEndpointProfileName = name;
            AssetEndpointProfile = assetEndpointProfile;
        }
    }
}
