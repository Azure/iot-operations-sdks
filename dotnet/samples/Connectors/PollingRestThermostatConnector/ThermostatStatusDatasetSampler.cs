// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RestThermostatConnector
{
    internal class ThermostatStatusDatasetSampler : IDatasetSampler, IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _assetName;
        private readonly EndpointCredentials? _credentials;

        private readonly static JsonSerializerOptions _jsonSerializerOptions = new()
        {
            AllowTrailingCommas = true,
        };

        public ThermostatStatusDatasetSampler(HttpClient httpClient, string assetName, EndpointCredentials? credentials)
        {
            _httpClient = httpClient;
            _assetName = assetName;
            _credentials = credentials;
        }

        /// <summary>
        /// Sample the datapoints from the HTTP thermostat and return the full serialized dataset.
        /// </summary>
        /// <param name="dataset">The dataset of an asset to sample.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The serialized payload containing the sampled dataset.</returns>
        public async Task<byte[]> SampleDatasetAsync(AssetDataset dataset, CancellationToken cancellationToken = default)
        {
            try
            {
                AssetDatasetDataPointSchemaElement httpServerDesiredTemperatureDataPoint = dataset.DataPoints!.Where(x => x.Name!.Equals("desiredTemperature"))!.First();
                HttpMethod httpServerDesiredTemperatureHttpMethod = HttpMethod.Parse(JsonSerializer.Deserialize<DataPointConfiguration>(httpServerDesiredTemperatureDataPoint.DataPointConfiguration!, _jsonSerializerOptions)!.HttpRequestMethod);
                string httpServerDesiredTemperatureRequestPath = httpServerDesiredTemperatureDataPoint.DataSource!;

                AssetDatasetDataPointSchemaElement httpServerCurrentTemperatureDataPoint = dataset.DataPoints!.Where(x => x.Name!.Equals("currentTemperature"))!.First();
                HttpMethod httpServerCurrentTemperatureHttpMethod = HttpMethod.Parse(JsonSerializer.Deserialize<DataPointConfiguration>(httpServerDesiredTemperatureDataPoint.DataPointConfiguration!, _jsonSerializerOptions)!.HttpRequestMethod);
                string httpServerCurrentTemperatureRequestPath = httpServerCurrentTemperatureDataPoint.DataSource!;

                if (_credentials != null && _credentials.Username != null && _credentials.Password != null)
                {
                    // Note that this sample uses username + password for authenticating the connection to the asset. In general,
                    // x509 authentication should be used instead (if available) as it is more secure.
                    string httpServerUsername = _credentials.Username;
                    string httpServerPassword = _credentials.Password;
                    var byteArray = Encoding.ASCII.GetBytes($"{httpServerUsername}:{httpServerPassword}");
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                }

                // In this sample, both the datapoints have the same datasource, so only one HTTP request is needed.
                var currentTemperatureHttpResponse = await _httpClient.GetAsync(httpServerCurrentTemperatureRequestPath);
                var desiredTemperatureHttpResponse = await _httpClient.GetAsync(httpServerDesiredTemperatureRequestPath);

                if (currentTemperatureHttpResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    || desiredTemperatureHttpResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Failed to authorize request to HTTP server. Check credentials configured in rest-server-device-definition.yaml.");
                }

                currentTemperatureHttpResponse.EnsureSuccessStatusCode();
                desiredTemperatureHttpResponse.EnsureSuccessStatusCode();

                ThermostatStatus thermostatStatus = new()
                {
                    CurrentTemperature = (JsonSerializer.Deserialize<ThermostatStatus>(await currentTemperatureHttpResponse.Content.ReadAsStreamAsync(), _jsonSerializerOptions)!).CurrentTemperature,
                    DesiredTemperature = (JsonSerializer.Deserialize<ThermostatStatus>(await desiredTemperatureHttpResponse.Content.ReadAsStreamAsync(), _jsonSerializerOptions)!).DesiredTemperature
                };

                // The HTTP response payload matches the expected message schema, so return it as-is
                return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(thermostatStatus));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to sample dataset with name {dataset.Name} in asset with name {_assetName}", ex);
            }
        }

        public Task<TimeSpan> GetSamplingIntervalAsync(AssetDataset dataset, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TimeSpan.FromSeconds(3));
        }

        public ValueTask DisposeAsync()
        {
            _httpClient.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
