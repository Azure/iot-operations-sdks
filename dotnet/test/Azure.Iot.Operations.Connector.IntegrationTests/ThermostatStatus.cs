// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    public class ThermostatStatus
    {
        [JsonPropertyName("desiredTemperature")]
        public double? DesiredTemperature { get; set; }

        [JsonPropertyName("currentTemperature")]
        public double? CurrentTemperature { get; set; }
    }
}
