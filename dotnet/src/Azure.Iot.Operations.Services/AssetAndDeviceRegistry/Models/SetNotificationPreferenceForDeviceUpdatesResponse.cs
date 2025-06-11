namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class SetNotificationPreferenceForDeviceUpdatesResponse
    {
        /// <summary>
        /// The 'responsePayload' Field.
        /// </summary>
        [JsonPropertyName("responsePayload")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ResponsePayload { get; set; } = default;

        /// <summary>
        /// The 'setNotificationPreferenceForDeviceUpdatesError' Field.
        /// </summary>
        [JsonPropertyName("setNotificationPreferenceForDeviceUpdatesError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? SetNotificationPreferenceForDeviceUpdatesError { get; set; } = default;

    }
}
