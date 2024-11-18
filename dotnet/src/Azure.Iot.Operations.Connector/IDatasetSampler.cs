﻿using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A sampler of a single dataset within an asset. For an example, see the HttpServerConnectorApp sample code.
    /// </summary>
    public interface IDatasetSampler
    {
        /// <summary>
        /// Sample the datapoints from the asset and return the full serialized dataset.
        /// </summary>
        /// <param name="dataset">The dataset of an asset to sample.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The serialized payload containing the sampled dataset.</returns>
        /// <remarks>
        /// This method will be invoked by the <see cref="ConnectorWorker"/> each time that a dataset needs to be sampled. The worker service
        /// will then forward the returned serialized payload to the MQTT broker stamped with cloud event headers.
        /// </remarks>
        public Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default);
    }
}
