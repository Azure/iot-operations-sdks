// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace SqlQualityAnalyzerConnectorApp
{
    public class SqlQualityAnalyzerDatasetSamplerProvider : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> Factory = service =>
        {
            return new SqlQualityAnalyzerDatasetSamplerProvider();
        };

        public IDatasetSampler CreateDatasetSampler(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, AssetDataset dataset, EndpointCredentials? deviceCredentials)
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
