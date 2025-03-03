// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// The event args for when an asset becomes unavailable to sample.
    /// </summary>
    public class AssetUnavailableEventArgs : EventArgs
    {
        /// <summary>
        /// The name of the asset that is no longer available to sample.
        /// </summary>
        public string AssetName { get; }

        internal AssetUnavailableEventArgs(string assetName)
        {
            AssetName = assetName;
        }
    }
}
