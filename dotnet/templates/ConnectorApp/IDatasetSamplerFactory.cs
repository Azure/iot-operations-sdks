using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace ConnectorAppProjectTemplate
{
    /// <summary>
    /// Factory interface for creating <see cref="IDatasetSampler"/> instances. For an example, see the HttpThermostatConnectorApp sample code.
    /// </summary>
    public interface IDatasetSamplerFactory
    {
        /// <summary>
        /// Factory method for creating a sampler for the provided dataset.
        /// </summary>
        /// <param name="assetEndpointProfile">The endpoint that holds the data to sample</param>
        /// <param name="asset">The asset that this dataset belongs to.</param>
        /// <param name="dataset">The dataset that the returned sampler will sample.</param>
        /// <returns>The dataset sampler that will be used everytime this dataset needs to be sampled.</returns>
        /// <remarks>
        /// When an asset is discovered, this application will automatically begin periodically sampling each of the datasets within that asset
        /// using a <see cref="IDatasetSampler"/> instance created by this method.
        /// </remarks>
        public IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset);
    }
}
