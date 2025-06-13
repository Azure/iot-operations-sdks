// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Assets;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace SqlQualityAnalyzerConnectorApp
{
    public class SqlQualityAnalyzerDatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> DatasetSamplerFactoryProvider = service =>
        {
            return new SqlQualityAnalyzerDatasetSamplerFactory();
        };

        public IDatasetSampler CreateDatasetSampler(Device device, string inboundEndpointName, string assetName, Asset asset, AssetDataset dataset, EndpointCredentials? deviceCredentials)
        {
            if (dataset.Name.Equals("qualityanalyzer_data"))
            {
                if (device.Endpoints != null
                    && device.Endpoints.Inbound != null
                    && device.Endpoints.Inbound.TryGetValue(inboundEndpointName, out var inboundEndpoint))
                {
                    string connectionString = inboundEndpoint.Address;
                    return new QualityAnalyzerDatasetSampler(connectionString, assetName, deviceCredentials);
                }
            }

            throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name} on asset with name {assetName}");
        }
    }
}
