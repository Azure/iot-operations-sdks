
namespace Azure.Iot.Operations.GenericHttpConnectorSample
{
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
