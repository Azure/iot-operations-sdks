using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class DatasetSamplerFactory : IDatasetSamplerFactory
    {
        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            return new DatasetSampler();
        }
    }
}
