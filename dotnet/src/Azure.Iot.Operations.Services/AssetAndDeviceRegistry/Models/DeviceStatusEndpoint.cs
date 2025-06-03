namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DeviceStatusEndpoint
    {
        /// <summary>
        /// The 'inbound' Field.
        /// </summary>
        [JsonPropertyName("inbound")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>? Inbound { get; set; } = default;

    }
}
