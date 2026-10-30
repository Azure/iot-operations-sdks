// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace PollingTelemetryConnectorTemplate
{
    /// <summary>
    /// The factory for creating the samplers for each dataset.
    /// </summary>
    public class DatasetSamplerProvider : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> Factory = service =>
        {
            return new DatasetSamplerProvider();
        };

        public IDatasetSampler CreateDatasetSampler(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, AssetDataset dataset, EndpointCredentials? endpointCredentials)
        {
            // As the connector discovers each dataset, it will need to know how to sample that dataset. To figure that
            // out, this callback is invoked to provide context on which dataset was discovered and what device + endpoint + asset it
            // belongs to. This callback may be called multiple times if the asset or dataset changes in any way over time.
            throw new NotImplementedException();
        }
    }
}
