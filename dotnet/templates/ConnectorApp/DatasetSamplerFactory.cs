using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace ConnectorApp
{
    public class DatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> DatasetSampleFactoryProvider = service =>
        {
            return new DatasetSamplerFactory();
        };

        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            throw new NotImplementedException();
        }
    }
}
