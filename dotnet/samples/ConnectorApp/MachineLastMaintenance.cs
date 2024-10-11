using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HttpConnectorWorkerService
{
    public class MachineLastMaintenance
    {
        [JsonPropertyName("machine_id")]
        public string MachineId { get; set; }

        [JsonPropertyName("last_maintenance")]
        public DateTime LastMaintenance { get; set; }

        public MachineLastMaintenance(string machineId, string lastMaintenance)
        {
            MachineId = machineId;
            LastMaintenance = DateTime.Parse(lastMaintenance);
        }
    }
}
