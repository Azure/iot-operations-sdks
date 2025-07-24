// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace RestThermostatConnector
{
    public class RestThermostatDatasetSamplerProvider : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> Factory = service =>
        {
            return new RestThermostatDatasetSamplerProvider();
        };

        /// <summary>
        /// Creates a dataset sampler for the given dataset.
        /// </summary>
        /// <param name="device">The device to connect to when sampling this dataset.</param>
        /// <param name="asset">The asset that the dataset sampler will sample from.</param>
        /// <param name="dataset">The dataset that a sampler is needed for.</param>
        /// <param name="authentication">The authentication to use when connecting to the device with this asset.</param>
        /// <returns>The dataset sampler for the provided dataset.</returns>
        public IDatasetSampler CreateDatasetSampler(string deviceName, Device device, string inboundEndpointName, string assetName, Asset asset, AssetDataset dataset, EndpointCredentials? credentials)
        {
            if (dataset.Name.Equals("thermostat_status"))
            {
                if (device.Endpoints != null
                    && device.Endpoints.Inbound != null
                    && device.Endpoints.Inbound.TryGetValue(inboundEndpointName, out var inboundEndpoint))
                {
                    var httpClient = new HttpClient()
                    {
                        BaseAddress = new Uri(inboundEndpoint.Address),
                    };

                    return new ThermostatStatusDatasetSampler(httpClient, assetName, credentials);
                }
            }

            throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name} on asset with name {assetName}");
        }
    }
}
