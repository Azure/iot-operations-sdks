// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets
{
    public class AssetEndpointProfileDeletedEventArgs : EventArgs
    {
        public string AssetEndpointProfileName { get; set; }

        internal AssetEndpointProfileDeletedEventArgs(string name)
        {
            AssetEndpointProfileName = name;
        }
    }
}
