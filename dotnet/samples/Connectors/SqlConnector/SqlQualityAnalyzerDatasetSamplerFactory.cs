// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace SqlQualityAnalyzerConnectorApp
{
    public class SqlQualityAnalyzerDatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> DatasetSamplerFactoryProvider = service =>
        {
            return new SqlQualityAnalyzerDatasetSamplerFactory();
        };

        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, AssetDatasetSchemaElement dataset)
        {
            if (dataset.Name.Equals("qualityanalyzer_data"))
            {
                string connectionString = assetEndpointProfile.Specification.TargetAddress;

                return new QualityAnalyzerDatasetSampler(connectionString, asset.Name, assetEndpointProfile.Specification.Authentication);

            }
            else
            {
                throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name} on asset with name {asset.Name}");
            }
        }
    }
}
