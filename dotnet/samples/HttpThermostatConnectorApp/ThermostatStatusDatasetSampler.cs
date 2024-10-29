// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.GenericHttpConnectorSample;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.ConnectorSample
{
    internal class ThermostatStatusDatasetSampler : IDatasetSampler, IDisposable
    {
        private HttpClient _httpClient;

        public ThermostatStatusDatasetSampler(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<byte[]> SampleAsync(AssetEndpointProfile assetEndpointProfile, Dataset dataset, CancellationToken cancellationToken = default)
        {
            string httpServerUsername = assetEndpointProfile!.Credentials!.Username!;
            byte[] httpServerPassword = assetEndpointProfile.Credentials!.Password!;

            DataPoint httpServerDesiredTemperatureDataPoint = dataset.DataPointsDictionary!["desired_temperature"];
            HttpMethod httpServerDesiredTemperatureHttpMethod = HttpMethod.Parse(httpServerDesiredTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
            string httpServerDesiredTemperatureRequestPath = httpServerDesiredTemperatureDataPoint.DataSource!;

            DataPoint httpServerActualTemperatureDataPoint = dataset.DataPointsDictionary!["actual_temperature"];
            HttpMethod httpServerActualTemperatureHttpMethod = HttpMethod.Parse(httpServerActualTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
            string httpServerActualTemperatureRequestPath = httpServerActualTemperatureDataPoint.DataSource!;

            var byteArray = Encoding.ASCII.GetBytes($"{httpServerUsername}:{Encoding.UTF8.GetString(httpServerPassword)}");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            // In this sample, both the datapoints have the same datasource, so only one HTTP request is needed.
            var response = await _httpClient.GetAsync(httpServerActualTemperatureRequestPath);

            // The HTTP response payload matches the expected message schema, so return it as-is
            return Encoding.UTF8.GetBytes(await response.Content.ReadAsStringAsync());
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
