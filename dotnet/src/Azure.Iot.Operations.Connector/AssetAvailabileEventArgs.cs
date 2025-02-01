// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class AssetAvailabileEventArgs : EventArgs
    {
        public string AssetName { get; }

        public Asset Asset { get; }

        internal AssetAvailabileEventArgs(string assetName, Asset asset)
        {
            AssetName = assetName;
            Asset = asset;
        }
    }
}
