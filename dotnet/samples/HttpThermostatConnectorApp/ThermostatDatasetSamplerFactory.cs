using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace HttpThermostatConnectorAppProjectTemplate
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
        /// <param name="dataset">The dataset that a sampler is needed for.</param>
        /// <returns>The dataset sampler for the provided dataset.</returns>
        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset)
        {
            if (asset.DisplayName!.Equals("My HTTP Thermostat Asset") && dataset.Name.Equals("thermostat_status"))
            {
                var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri(assetEndpointProfile.TargetAddress),
                };

                return new ThermostatStatusDatasetSampler(httpClient, asset.DisplayName!);
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name} on asset with name {asset.DisplayName}");
            }
        }
    }
}
