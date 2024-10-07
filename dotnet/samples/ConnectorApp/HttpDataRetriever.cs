// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DotnetHttpConnectorWorkerService
{
    internal class HttpDataRetriever
    {
        private readonly HttpClient _httpClient;
        private readonly string _httpPath;
        private readonly string _httpServerUsername;
        private readonly byte[] _httpServerPassword;
        private static readonly TimeSpan _defaultOperationTimeout = TimeSpan.FromSeconds(100);
        private bool _disposed = false;

        public HttpDataRetriever(string httpServerAddress, string httpPath, HttpMethod httpMethod, string httpServerUsername, byte[] httpServerPassword)
        {
            if (httpMethod != HttpMethod.Get)
            {
                throw new NotSupportedException("Unexpected HTTP method configured. Only GET is supported in this sample");
            }

            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(httpServerAddress),
                Timeout = _defaultOperationTimeout
            };

            _httpPath = httpPath;
            _httpServerUsername = httpServerUsername;
            _httpServerPassword = httpServerPassword;
        }

        private void Authenticate()
        {
            var byteArray = Encoding.ASCII.GetBytes($"{_httpServerUsername}:{Encoding.UTF8.GetString(_httpServerPassword)}");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        public async Task<string> RetrieveDataAsync()
        {
            // Implement HTTP data retrieval logic
            Authenticate();
            var response = await _httpClient.GetAsync(_httpPath);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                throw new HttpRequestException($"Request to {_httpClient.BaseAddress} failed with status code {response.StatusCode}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
