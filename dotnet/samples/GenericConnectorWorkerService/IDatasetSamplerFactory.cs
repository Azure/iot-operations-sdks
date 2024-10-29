using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace Azure.Iot.Operations.GenericHttpConnectorSample
{
    public interface IDatasetSamplerFactory
    {
        public IDatasetSampler ConstructSampler(AssetEndpointProfile assetEndpointProfile, Dataset dataset);
    }
}
