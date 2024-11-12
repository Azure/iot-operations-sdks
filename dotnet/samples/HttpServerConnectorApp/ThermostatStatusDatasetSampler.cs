using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;
using System.Net.Http.Headers;
using System.Text;

namespace HttpServerConnectorApp
{
    internal class ThermostatStatusDatasetSampler : IDatasetSampler
    {
        private HttpClient _httpClient;
        private string _assetName;
        private AssetEndpointProfileCredentials _credentials;

        public ThermostatStatusDatasetSampler(HttpClient httpClient, string assetName, AssetEndpointProfileCredentials credentials)
        {
            _httpClient = httpClient;
            _assetName = assetName;
            _credentials = credentials;
        }

        /// <summary>
        /// Sample the datapoints from the HTTP thermostat and return the full serialized dataset.
        /// </summary>
        /// <param name="dataset">The dataset of an asset to sample.</param>
        /// <param name="assetEndpointProfileCredentials">The credentials to use when sampling the asset. May be null if no credentials are required.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The serialized payload containing the sampled dataset.</returns>
        public async Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            try
            {
                DataPoint httpServerDesiredTemperatureDataPoint = dataset.DataPointsDictionary!["desired_temperature"];
                HttpMethod httpServerDesiredTemperatureHttpMethod = HttpMethod.Parse(httpServerDesiredTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerDesiredTemperatureRequestPath = httpServerDesiredTemperatureDataPoint.DataSource!;

                DataPoint httpServerActualTemperatureDataPoint = dataset.DataPointsDictionary!["actual_temperature"];
                HttpMethod httpServerActualTemperatureHttpMethod = HttpMethod.Parse(httpServerActualTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerActualTemperatureRequestPath = httpServerActualTemperatureDataPoint.DataSource!;

                if (_credentials != null)
                {
                    string httpServerUsername = _credentials.Username!;
                    byte[] httpServerPassword = _credentials.Password!;
                    var byteArray = Encoding.ASCII.GetBytes($"{httpServerUsername}:{Encoding.UTF8.GetString(httpServerPassword)}");
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                }

                // In this sample, both the datapoints have the same datasource, so only one HTTP request is needed.
                var response = await _httpClient.GetAsync(httpServerActualTemperatureRequestPath);

                // The HTTP response payload matches the expected message schema, so return it as-is
                return Encoding.UTF8.GetBytes(await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to sample dataset with name {dataset.Name} in asset with name {_assetName}", ex);
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
