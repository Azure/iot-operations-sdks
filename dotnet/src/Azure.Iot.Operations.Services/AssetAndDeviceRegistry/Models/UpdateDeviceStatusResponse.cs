namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class UpdateDeviceStatusResponse
    {
        /// <summary>
        /// The 'updatedDeviceStatus' Field.
        /// </summary>
        [JsonPropertyName("updatedDeviceStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DeviceStatus? UpdatedDeviceStatus { get; set; } = default;

        /// <summary>
        /// The 'updateDeviceStatusError' Field.
        /// </summary>
        [JsonPropertyName("updateDeviceStatusError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? UpdateDeviceStatusError { get; set; } = default;

    }
}
