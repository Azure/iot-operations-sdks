// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.GenericHttpConnectorSample;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System.Net.Http.Headers;
using System.Text;

namespace Azure.Iot.Operations.ConnectorSample
{
    /// <summary>
    /// An example dataset sampler. This dataset sampler is designed to sample the "thermostat_status" dataset within the "my-http-thermostat-asset" asset.
    /// </summary>
    internal class ThermostatStatusDatasetSampler : IDatasetSampler, IDisposable
    {
        private HttpClient _httpClient;

        public ThermostatStatusDatasetSampler(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Sample the datapoints from the HTTP thermostat and return the full serialized dataset.
        /// </summary>
        /// <param name="dataset">The dataset of an asset to sample.</param>
        /// <param name="assetEndpointProfileCredentials">The credentials to use when sampling the asset. May be null if no credentials are required.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The serialized payload containing the sampled dataset.</returns>
        public async Task<byte[]> SampleAsync(Dataset dataset, AssetEndpointProfileCredentials? assetEndpointProfileCredentials = null, CancellationToken cancellationToken = default)
        {
            DataPoint httpServerDesiredTemperatureDataPoint = dataset.DataPointsDictionary!["desired_temperature"];
            HttpMethod httpServerDesiredTemperatureHttpMethod = HttpMethod.Parse(httpServerDesiredTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
            string httpServerDesiredTemperatureRequestPath = httpServerDesiredTemperatureDataPoint.DataSource!;

            DataPoint httpServerActualTemperatureDataPoint = dataset.DataPointsDictionary!["actual_temperature"];
            HttpMethod httpServerActualTemperatureHttpMethod = HttpMethod.Parse(httpServerActualTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
            string httpServerActualTemperatureRequestPath = httpServerActualTemperatureDataPoint.DataSource!;

            if (assetEndpointProfileCredentials != null)
            {
                string httpServerUsername = assetEndpointProfileCredentials.Username!;
                byte[] httpServerPassword = assetEndpointProfileCredentials.Password!;
                var byteArray = Encoding.ASCII.GetBytes($"{httpServerUsername}:{Encoding.UTF8.GetString(httpServerPassword)}");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            // In this sample, both the datapoints have the same datasource, so only one HTTP request is needed.
            var response = await _httpClient.GetAsync(httpServerActualTemperatureRequestPath);

            // The HTTP response payload matches the expected message schema, so return it as-is
            return Encoding.UTF8.GetBytes(await response.Content.ReadAsStringAsync());
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
