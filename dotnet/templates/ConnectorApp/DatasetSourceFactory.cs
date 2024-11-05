using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace ConnectorAppProjectTemplate
{
    public class DatasetSourceFactory : IDatasetSourceFactory
    {
        public static Func<IServiceProvider, IDatasetSourceFactory> DatasetSourceFactoryProvider = service =>
        {
            throw new NotImplementedException();
        };

        public IDatasetSource CreateDatasetSource(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            throw new NotImplementedException();
        }
    }
}
