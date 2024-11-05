using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace HttpServerConnectorApp
{
    /// <summary>
    /// Factory interface for creating <see cref="IDatasetSampler"/> instances. For an example, see the HttpThermostatHttpThermostatConnectorApp sample code.
    /// </summary>
    public interface IDatasetSamplerFactory
    {
        /// <summary>
        /// Factory method for creating a sampler for the provided dataset.
        /// </summary>
        /// <param name="assetEndpointProfile">The endpoint that holds the data to sample</param>
        /// <param name="dataset">The dataset that the returned sampler will sample.</param>
        /// <returns>The dataset sampler that will be used everytime this dataset needs to be sampled.</returns>
        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset);
    }
}
