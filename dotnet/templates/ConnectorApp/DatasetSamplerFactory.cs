using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace ConnectorAppProjectTemplate
{
    public class DatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> DatasetSamplerFactoryProvider = service =>
        {
            throw new NotImplementedException();
        };

        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Dataset dataset)
        {
            throw new NotImplementedException();
        }
    }
}
