using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpConnectorWorkerService
{
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
