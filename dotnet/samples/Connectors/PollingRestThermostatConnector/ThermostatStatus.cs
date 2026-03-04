// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace RestThermostatConnector
{
    public class ThermostatStatus
    {
        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("humidity")]
        public double? Humidity { get; set; }
    }
}
