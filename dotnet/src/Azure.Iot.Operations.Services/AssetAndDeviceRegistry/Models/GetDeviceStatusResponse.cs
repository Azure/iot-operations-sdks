namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class GetDeviceStatusResponse
    {
        /// <summary>
        /// The 'deviceStatus' Field.
        /// </summary>
        [JsonPropertyName("deviceStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DeviceStatus? DeviceStatus { get; set; } = default;

        /// <summary>
        /// The 'getDeviceStatusError' Field.
        /// </summary>
        [JsonPropertyName("getDeviceStatusError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? GetDeviceStatusError { get; set; } = default;

    }
}
