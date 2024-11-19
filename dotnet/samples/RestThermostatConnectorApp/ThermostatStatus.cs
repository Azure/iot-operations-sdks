using System.Text.Json.Serialization;

namespace RestThermostatConnector
{
    internal class ThermostatStatus
    {
        [JsonPropertyName("desiredTemperature")]
        public double? DesiredTemperature { get; set; }

        [JsonPropertyName("currentTemperature")]
        public double? CurrentTemperature { get; set; }
    }
}
