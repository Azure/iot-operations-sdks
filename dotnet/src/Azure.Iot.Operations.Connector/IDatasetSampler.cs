using Azure.Iot.Operations.Services.Assets;
using SchemaInfo = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Object_Ms_Adr_SchemaRegistry_Schema__1;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A sampler of a single dataset within an asset.
    /// </summary>
    public interface IDatasetSampler : IAsyncDisposable
    {
        public Task<DatasetMessageSchema?> GetMessageSchemaAsync(Dataset dataset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sample the datapoints from the asset and return the full serialized dataset.
        /// </summary>
        /// <param name="dataset">The dataset of an asset to sample.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The serialized payload containing the sampled dataset.</returns>
        /// <remarks>
        /// This method will be invoked by the <see cref="TelemetryConnectorWorker"/> each time that a dataset needs to be sampled. The worker service
        /// will then forward the returned serialized payload to the MQTT broker stamped with cloud event headers.
        /// </remarks>
        public Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default);
    }
}
