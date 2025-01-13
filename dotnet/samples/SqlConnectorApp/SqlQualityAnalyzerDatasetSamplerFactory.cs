using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

namespace SqlQualityAnalyzerConnectorApp
{
    public class SqlQualityAnalyzerDatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> DatasetSamplerFactoryProvider = service =>
        {
            //var loggerFactory = service.GetRequiredService<ILoggerFactory>();
            //var logger = loggerFactory.CreateLogger<SqlQualityAnalyzerDatasetSamplerFactory>();
            return new SqlQualityAnalyzerDatasetSamplerFactory();
        };
        // private readonly ILogger<SqlQualityAnalyzerDatasetSamplerFactory> _logger;
        // public SqlQualityAnalyzerDatasetSamplerFactory(ILogger<SqlQualityAnalyzerDatasetSamplerFactory> logger)
        // {
        //     _logger = logger;
        // }

        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            if (dataset.Name.Equals("qualityanalyzer_data"))
            {
                string connectionString = assetEndpointProfile.TargetAddress;

                return new QualityAnalyzerDatasetSampler(connectionString, asset.DisplayName!, assetEndpointProfile.Credentials);
                // _loggerFactory.CreateLogger<QualityAnalyzerDatasetSampler>()

            }
            else
            {
                throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name} on asset with name {asset.DisplayName}");
            }
        }
    }
}
