﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.ConnectorSample
{
    internal class HttpDataRetriever : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _httpPath;
        private readonly string _httpServerUsername;
        private readonly byte[] _httpServerPassword;
        private static readonly TimeSpan _defaultOperationTimeout = TimeSpan.FromSeconds(100);

        public HttpDataRetriever(string httpServerAddress, string httpPath, HttpMethod httpMethod, string httpServerUsername, byte[] httpServerPassword)
        {
            // In a more complex sample, this class would support doing put/post/delete/etc but logic for handling that has been omitted
            // for brevity.
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

        public async Task<string> RetrieveDataAsync(string propertyName)
        {
            // Add authorization to the HTTP request
            var byteArray = Encoding.ASCII.GetBytes($"{_httpServerUsername}:{Encoding.UTF8.GetString(_httpServerPassword)}");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            
            var response = await _httpClient.GetAsync(_httpPath);
            string jsonResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine(jsonResponse);
            //TODO feels bad making the same HTTP request multiple times to get a single property from each response. Does ADR have something for allowing mutliple property names to be read?
            return JsonDocument.Parse(jsonResponse).RootElement.GetProperty(propertyName).GetString()!;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
