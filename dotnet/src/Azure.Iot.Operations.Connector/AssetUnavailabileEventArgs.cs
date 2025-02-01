// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    public class AssetUnavailabileEventArgs : EventArgs
    {
        public string AssetName { get; }

        internal AssetUnavailabileEventArgs(string assetName)
        {
            AssetName = assetName;
        }
    }
}
