namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DeviceStatus
    {
        /// <summary>
        /// The 'config' Field.
        /// </summary>
        [JsonPropertyName("config")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DeviceStatusConfig? Config { get; set; } = default;

        /// <summary>
        /// Defines the device status for inbound/outbound endpoints.
        /// </summary>
        [JsonPropertyName("endpoints")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DeviceStatusEndpoint? Endpoints { get; set; } = default;

    }
}
