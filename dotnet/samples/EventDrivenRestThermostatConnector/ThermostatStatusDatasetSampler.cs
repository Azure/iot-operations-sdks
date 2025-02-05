// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RestThermostatConnector
{
    internal class ThermostatStatusDatasetSampler : IDatasetSampler
    {
        private HttpClient _httpClient;
        private string _assetName;
        private AssetEndpointProfileCredentials? _credentials;

        public ThermostatStatusDatasetSampler(HttpClient httpClient, string assetName, AssetEndpointProfileCredentials? credentials)
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
                DataPoint httpServerDesiredTemperatureDataPoint = dataset.DataPointsDictionary!["desiredTemperature"];
                HttpMethod httpServerDesiredTemperatureHttpMethod = HttpMethod.Parse(httpServerDesiredTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerDesiredTemperatureRequestPath = httpServerDesiredTemperatureDataPoint.DataSource!;

                DataPoint httpServerCurrentTemperatureDataPoint = dataset.DataPointsDictionary!["currentTemperature"];
                HttpMethod httpServerCurrentTemperatureHttpMethod = HttpMethod.Parse(httpServerCurrentTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerCurrentTemperatureRequestPath = httpServerCurrentTemperatureDataPoint.DataSource!;

                if (_credentials != null)
                {
                    string httpServerUsername = _credentials.Username!;
                    byte[] httpServerPassword = _credentials.Password!;
                    var byteArray = Encoding.ASCII.GetBytes($"{httpServerUsername}:{Encoding.UTF8.GetString(httpServerPassword)}");
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                }

                // In this sample, both the datapoints have the same datasource, so only one HTTP request is needed.
                var currentTemperatureHttpResponse = await _httpClient.GetAsync(httpServerCurrentTemperatureRequestPath);
                var desiredTemperatureHttpResponse = await _httpClient.GetAsync(httpServerDesiredTemperatureRequestPath);

                ThermostatStatus thermostatStatus = new();
                thermostatStatus.CurrentTemperature = (JsonSerializer.Deserialize<ThermostatStatus>(await currentTemperatureHttpResponse.Content.ReadAsStreamAsync())!).CurrentTemperature;
                thermostatStatus.DesiredTemperature = (JsonSerializer.Deserialize<ThermostatStatus>(await desiredTemperatureHttpResponse.Content.ReadAsStreamAsync())!).DesiredTemperature;

                // The HTTP response payload matches the expected message schema, so return it as-is
                return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(thermostatStatus));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to sample dataset with name {dataset.Name} in asset with name {_assetName}", ex);
            }
        }

        public Task<DatasetMessageSchema?> GetMessageSchemaAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this dataset.
            return Task.FromResult((DatasetMessageSchema?)null);
        }

        public ValueTask DisposeAsync()
        {
            _httpClient.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
