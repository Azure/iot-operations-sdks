// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A bundle of asset name + dataset name in one class to fit how <see cref="Timer"/> passes around context
    /// </summary>
    internal class TimerContext
    {
        internal AssetEndpointProfile AssetEndpointProfile { get; set; }

        internal Asset Asset { get; set; }

        internal string AssetName { get; set; }

        internal string DatasetName { get; set; }

        internal CancellationToken CancellationToken { get; set; }

        internal TimerContext(AssetEndpointProfile assetEndpointProfile, Asset asset, string assetName, string datasetName, CancellationToken cancellationToken)
        {
            AssetEndpointProfile = assetEndpointProfile;
            Asset = asset;
            AssetName = assetName;
            DatasetName = datasetName;
            CancellationToken = cancellationToken;
        }
    }
}
