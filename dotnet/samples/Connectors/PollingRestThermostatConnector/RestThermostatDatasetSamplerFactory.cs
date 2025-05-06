// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Assets;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace RestThermostatConnector
{
    public class RestThermostatDatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> RestDatasetSourceFactoryProvider = service =>
        {
            return new RestThermostatDatasetSamplerFactory();
        };

        /// <summary>
        /// Creates a dataset sampler for the given dataset.
        /// </summary>
        /// <param name="device">The device to connect to when sampling this dataset.</param>
        /// <param name="asset">The asset that the dataset sampler will sample from.</param>
        /// <param name="dataset">The dataset that a sampler is needed for.</param>
        /// <param name="authentication">The authentication to use when connecting to the device with this asset.</param>
        /// <returns>The dataset sampler for the provided dataset.</returns>
        public IDatasetSampler CreateDatasetSampler(Device device, string inboundEndpointName, Asset asset, AssetDatasetSchemaElement dataset, EndpointCredentials? credentials)
        {
            if (dataset.Name.Equals("thermostat_status"))
            {
                if (device.Specification.Endpoints != null
                    && device.Specification.Endpoints.Inbound != null
                    && device.Specification.Endpoints.Inbound.TryGetValue(inboundEndpointName, out var inboundEndpoint))
                {
                    var httpClient = new HttpClient()
                    {
                        BaseAddress = new Uri(inboundEndpoint.Address),
                    };

                    return new ThermostatStatusDatasetSampler(httpClient, asset.Name, credentials);
                }
            }

            throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name} on asset with name {asset.Name}");
        }
    }
}
