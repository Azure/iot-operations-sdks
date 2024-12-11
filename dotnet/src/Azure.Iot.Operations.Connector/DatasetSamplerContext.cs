
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A bundle of asset name + dataset name in one class to fit how <see cref="Timer"/> passes around context
    /// </summary>
    internal class DatasetSamplerContext
    {
        internal AssetEndpointProfile AssetEndpointProfile { get; set; }

        internal Asset Asset { get; set; }

        internal string DatasetName { get; set; }

        internal CancellationToken CancellationToken { get; set; }

        internal DatasetSamplerContext(AssetEndpointProfile assetEndpointProfile, Asset asset, string datasetName, CancellationToken cancellationToken)
        {
            AssetEndpointProfile = assetEndpointProfile;
            Asset = asset;
            DatasetName = datasetName;
            CancellationToken = cancellationToken;
        }
    }
}
