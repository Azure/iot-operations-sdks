// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Factory interface for creating <see cref="IDatasetSampler"/> instances.
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
        IDatasetSampler CreateDatasetSampler(AssetEndpointProfile assetEndpointProfile, Asset asset, Dataset dataset);
    }
}
