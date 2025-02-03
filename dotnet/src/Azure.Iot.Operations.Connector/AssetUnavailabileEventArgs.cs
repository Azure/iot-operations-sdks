// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// The event args for when an asset becomes unavailable to sample.
    /// </summary>
    public class AssetUnavailabileEventArgs : EventArgs
    {
        /// <summary>
        /// The name of the asset that is no longer available to sample.
        /// </summary>
        public string AssetName { get; }

        internal AssetUnavailabileEventArgs(string assetName)
        {
            AssetName = assetName;
        }
    }
}
