namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DeviceEndpoints
    {
        /// <summary>
        /// The 'inbound' Field.
        /// </summary>
        [JsonPropertyName("inbound")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, InboundSchemaMapValue>? Inbound { get; set; } = default;

        /// <summary>
        /// Set of endpoints for device to connect to.
        /// </summary>
        [JsonPropertyName("outbound")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public OutboundSchema? Outbound { get; set; } = default;

    }
}
