﻿using Azure.Iot.Operations.Services.AzureDeviceRegistry;

namespace ConnectorApp
{
    /// <summary>
    /// Factory interface for creating <see cref="IDatasetSource"/> instances. For an example, see the HttpServerConnectorApp sample code.
    /// </summary>
    public interface IDatasetSourceFactory
    {
        /// <summary>
        /// Factory method for creating a sampler for the provided dataset.
        /// </summary>
        /// <param name="assetEndpointProfile">The endpoint that holds the data to sample</param>
        /// <param name="asset">The asset that this dataset belongs to.</param>
        /// <param name="dataset">The dataset that the returned sampler will sample.</param>
        /// <returns>The dataset sampler that will be used everytime this dataset needs to be sampled.</returns>
        public IDatasetSource CreateDatasetSource(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset);
    }
}
