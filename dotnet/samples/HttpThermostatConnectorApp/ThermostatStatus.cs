using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.ConnectorSample
{
    /// <summary>
    /// This is the expected message schema for both the HTTP responses sent by the asset when sampled and the expected message schema of the MQTT 
    /// messages that the connector app will forward to the MQTT broker.
    /// </summary>
    public class ThermostatStatus
    {
        [JsonPropertyName("desired_temperature")]
        public string DesiredTemparature { get; set; }

        [JsonPropertyName("actual_temperature")]
        public string ActualTemperature { get; set; }

        public ThermostatStatus(string desiredTemparature, string actualTemperature)
        {
            DesiredTemparature = desiredTemparature;
            ActualTemperature = actualTemperature;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
