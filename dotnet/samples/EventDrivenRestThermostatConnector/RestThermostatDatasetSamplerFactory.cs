// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;

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
        /// <param name="assetEndpointProfile">The asset endpoint profile to connect to when sampling this dataset.</param>
        /// <param name="asset">The asset that the dataset sampler will sample from.</param>
        /// <param name="dataset">The dataset that a sampler is needed for.</param>
        /// <returns>The dataset sampler for the provided dataset.</returns>
        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            if (dataset.Name.Equals("thermostat_status"))
            {
                var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri(assetEndpointProfile.TargetAddress),
                };

                return new ThermostatStatusDatasetSampler(httpClient, asset.DisplayName!, assetEndpointProfile.Credentials);
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name} on asset with name {asset.DisplayName}");
            }
        }
    }
}
