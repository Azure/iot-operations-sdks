// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.GenericHttpConnectorSample;
using GenericHttpServerConnectorWorkerService;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.ConnectorSample
{
    internal class ThermostatStatusHttpServerSampler : IHttpServerDatasetSampler
    {
        private string? actual;
        private string? desired;

        public byte[] GetSerializedDatasetPayload(string datasetName)
        {
            if (datasetName.Equals("thermostat_status"))
            {
                if (actual == null || desired == null)
                {
                    throw new InvalidOperationException("Cannot get dataset payload because not all datapoints within that dataset have been retrieved yet.");
                }

                return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new ThermostatStatus(desired, actual)));
            }

            throw new NotImplementedException("Unrecognized dataset name: " + datasetName);
        }

        public async Task SampleAsync(HttpRequestContext httpRequestContext, string datasetName, CancellationToken cancellationToken = default)
        {
            if (httpRequestContext.HttpMethod != HttpMethod.Get)
            {
                throw new NotSupportedException("Unexpected HTTP method configured. Only GET is supported in this sample");
            }

            using var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(httpRequestContext.HttpServerAddress),
            };

            if (httpRequestContext.HttpServerUsername != null && httpRequestContext.HttpServerPassword != null)
            {
                // Add authorization to the HTTP request
                var byteArray = Encoding.ASCII.GetBytes($"{httpRequestContext.HttpServerUsername}:{Encoding.UTF8.GetString(httpRequestContext.HttpServerPassword)}");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            var response = await httpClient.GetAsync(httpRequestContext.HttpPath);
            string jsonResponse = await response.Content.ReadAsStringAsync();

            string value = JsonDocument.Parse(jsonResponse).RootElement.GetProperty(httpRequestContext.PropertyName).GetString()!;

            if (datasetName.Equals("thermostat_status"))
            {
                if (httpRequestContext.PropertyName.Equals("actual_temperature"))
                {
                    actual = value;
                }
                else
                {
                    desired = value;
                }
            }
            else
            {
                throw new NotImplementedException("Unrecognized dataset name: " + datasetName);
            }
        }
    }
}
