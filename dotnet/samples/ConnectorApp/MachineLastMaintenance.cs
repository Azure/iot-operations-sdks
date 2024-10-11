using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpConnectorWorkerService
{
    public class MachineLastMaintenance
    {
        [JsonPropertyName("last_maintenance")]
        public DateTime LastMaintenance { get; set; }

        public MachineLastMaintenance(string lastMaintenance)
        {
            LastMaintenance = DateTime.Parse(lastMaintenance);
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
