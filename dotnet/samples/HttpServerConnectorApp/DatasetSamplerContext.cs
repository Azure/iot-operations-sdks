
namespace HttpServerConnectorApp
{
    /// <summary>
    /// A bundle of asset name + dataset name in one class to fit how <see cref="Timer"/> passes around context
    /// </summary>
    internal class DatasetSamplerContext
    {
        internal string AssetName { get; set; }

        internal string DatasetName { get; set; }

        internal DatasetSamplerContext(string assetName, string datasetName)
        {
            AssetName = assetName;
            DatasetName = datasetName;
        }
    }
}
