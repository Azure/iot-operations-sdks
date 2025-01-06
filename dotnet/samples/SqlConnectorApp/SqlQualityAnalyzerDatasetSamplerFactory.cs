using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace SqlQualityAnalyzerConnectorApp
{
    public class SqlQualityAnalyzerDatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> DatasetSamplerFactoryProvider = service =>
        {
            return new SqlQualityAnalyzerDatasetSamplerFactory();
        };

        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            // this method should return the appropriate dataset sampler implementation for the provided asset + dataset. This
            // method may be called multiple times if the asset or dataset changes in any way over time.
            if (dataset.Name.Equals("qualityanalyzer_data"))
            {
                string connectionString = assetEndpointProfile.TargetAddress;

                return new QualityAnalyzerDatasetSampler(connectionString, asset.DisplayName!, assetEndpointProfile.Credentials);
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name} on asset with name {asset.DisplayName}");
            }
        }
    }
}
