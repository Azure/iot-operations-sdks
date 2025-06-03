namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DiscoveredDeviceEndpoints
    {
        /// <summary>
        /// The 'inbound' Field.
        /// </summary>
        [JsonPropertyName("inbound")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, DiscoveredDeviceInboundEndpoint>? Inbound { get; set; } = default;

        /// <summary>
        /// The 'outbound' Field.
        /// </summary>
        [JsonPropertyName("outbound")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DiscoveredDeviceOutboundEndpoints? Outbound { get; set; } = default;

    }
}
