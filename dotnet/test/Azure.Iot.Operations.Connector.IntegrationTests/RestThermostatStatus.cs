// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    public class RestThermostatStatus
    {
        [JsonPropertyName("currentTemperature")]
        public double? CurrentTemperature { get; set; }

        [JsonPropertyName("desiredTemperature")]
        public double? DesiredTemperature { get; set; }
    }
}
