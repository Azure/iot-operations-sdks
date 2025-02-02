// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


namespace Azure.Iot.Operations.Connector
{
    public class AssetUnavailabileEventArgs : EventArgs
    {
        public string AssetName { get; }

        public AssetUnavailabileEventArgs(string assetName)
        {
            AssetName = assetName;
        }
    }
}
