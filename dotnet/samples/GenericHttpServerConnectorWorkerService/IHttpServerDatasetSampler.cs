using GenericHttpServerConnectorWorkerService;
using System.Text.Json;

namespace Azure.Iot.Operations.GenericHttpConnectorSample
{
    // TODO Since the design is for this to handle sampling/serializing for X datasets, I should probably add another dataset to show how that would work.
    public interface IHttpServerDatasetSampler
    {
        /// <summary>
        /// Sample a single data point from an HTTP server and save the value of that data point. Once all data points have been sampled,
        /// <see cref="GetSerializedDatasetPayload"/> will return the serialized object comprised of those datapoints.
        /// </summary>
        /// <param name="httpRequestContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The value of the specified property in the HTTP response.</returns>
        public Task SampleAsync(HttpRequestContext httpRequestContext, string datasetName, CancellationToken cancellationToken = default);

        public byte[] GetSerializedDatasetPayload(string datasetName);
    }
}
