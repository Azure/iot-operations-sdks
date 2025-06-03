namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class GetDeviceResponse
    {
        /// <summary>
        /// The 'device' Field.
        /// </summary>
        [JsonPropertyName("device")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Device? Device { get; set; } = default;

        /// <summary>
        /// The 'getDeviceError' Field.
        /// </summary>
        [JsonPropertyName("getDeviceError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? GetDeviceError { get; set; } = default;

    }
}
