
namespace ConnectorAppProjectTemplate
{
    /// <summary>
    /// A bundle of asset name + dataset name in one class to fit how <see cref="Timer"/> passes around context
    /// </summary>
    internal class DatasetSourceContext
    {
        internal string AssetName { get; set; }

        internal string DatasetName { get; set; }

        internal DatasetSourceContext(string assetName, string datasetName)
        {
            AssetName = assetName;
            DatasetName = datasetName;
        }
    }
}
