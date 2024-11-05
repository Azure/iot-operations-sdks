using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace HttpServerConnectorApp
{
    public class ThermostatDatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> ThermostatDatasetSamplerFactoryProvider = service =>
        {
            return new ThermostatDatasetSamplerFactory();
        };

        /// <summary>
        /// Creates a dataset sampler for the given dataset.
        /// </summary>
        /// <param name="assetEndpointProfile">The asset endpoint profile to connect to when sampling this dataset.</param>
        /// <param name="asset">The asset that the dataset sampler will sample from.</param>
        /// <param name="dataset">The dataset that a sampler is needed for.</param>
        /// <returns>The dataset sampler for the provided dataset.</returns>
        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset) //TODO do credentials change over time? May just pass in creds here if not
        {
            if (asset.DisplayName!.Equals("My HTTP Thermostat Asset") && dataset.Name.Equals("thermostat_status"))
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
