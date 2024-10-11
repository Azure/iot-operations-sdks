using System.Text.Json.Serialization;

namespace HttpConnectorWorkerService
{
    public class MachineStatus
    {
        [JsonPropertyName("machine_id")]
        public string MachineId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        public MachineStatus(string machineId, string status)
        {
            MachineId = machineId;
            Status = status;
        }
    }
}
