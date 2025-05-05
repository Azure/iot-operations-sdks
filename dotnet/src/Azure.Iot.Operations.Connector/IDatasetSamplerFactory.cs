// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Assets;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

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
        /// <param name="device">The device that holds the asset to sample</param>
        /// <param name="asset">The asset that this dataset belongs to.</param>
        /// <param name="dataset">The dataset that the returned sampler will sample.</param>
        /// <param name="endpointCredentials">The authentication to use when connecting to the endpoint with this asset.</param>
        /// <returns>The dataset sampler that will be used everytime this dataset needs to be sampled.</returns>
        IDatasetSampler CreateDatasetSampler(Device device, string inboundEndpointName, Asset asset, AssetDatasetSchemaElement dataset, EndpointCredentials? endpointCredentials);
    }
}
