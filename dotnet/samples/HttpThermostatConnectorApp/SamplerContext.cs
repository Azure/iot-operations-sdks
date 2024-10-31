
namespace HttpThermostatConnectorAppProjectTemplate
{
    /// <summary>
    /// A bundle of asset name + dataset name in one class to fit how <see cref="Timer"/> passes around context
    /// </summary>
    internal class SamplerContext
    {
        internal string AssetName { get; set; }

        internal string DatasetName { get; set; }

        internal SamplerContext(string assetName, string datasetName)
        {
            AssetName = assetName;
            DatasetName = datasetName;
        }
    }
}
