// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace PollingTelemetryConnectorTemplate
{
    public class DatasetSamplerProvider : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> Factory = service =>
        {
            return new DatasetSamplerProvider();
        };

        public IDatasetSampler CreateDatasetSampler(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, AssetDataset dataset, EndpointCredentials? endpointCredentials)
        {
            // this method should return the appropriate dataset sampler implementation for the provided asset + dataset. This
            // method may be called multiple times if the asset or dataset changes in any way over time.
            throw new NotImplementedException();
        }
    }
}
