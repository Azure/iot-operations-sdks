// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace RestThermostatConnector
{
    public class DataPointConfiguration
    {
        [JsonPropertyName("HttpRequestMethod")]
        public string? HttpRequestMethod { get; set; }
    }
}
