
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A bundle of asset name + dataset name in one class to fit how <see cref="Timer"/> passes around context
    /// </summary>
    internal class DatasetSamplerContext
    {
        internal Asset Asset { get; set; }

        internal string DatasetName { get; set; }

        internal DatasetSamplerContext(Asset asset, string datasetName)
        {
            Asset = asset;
            DatasetName = datasetName;
        }
    }
}
