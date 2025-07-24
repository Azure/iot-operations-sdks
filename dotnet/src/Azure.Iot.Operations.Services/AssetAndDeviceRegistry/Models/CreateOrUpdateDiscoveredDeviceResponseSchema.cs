namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class CreateOrUpdateDiscoveredDeviceResponseSchema
    {
        /// <summary>
        /// The 'createOrUpdateDiscoveredDeviceError' Field.
        /// </summary>
        [JsonPropertyName("createOrUpdateDiscoveredDeviceError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? CreateOrUpdateDiscoveredDeviceError { get; set; } = default;

        /// <summary>
        /// The 'discoveredDeviceResponse' Field.
        /// </summary>
        [JsonPropertyName("discoveredDeviceResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DiscoveredDeviceResponseSchema? DiscoveredDeviceResponse { get; set; } = default;

    }
}

