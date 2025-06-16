namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class SetNotificationPreferenceForAssetUpdatesResponse
    {
        /// <summary>
        /// The 'responsePayload' Field.
        /// </summary>
        [JsonPropertyName("responsePayload")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ResponsePayload { get; set; } = default;

        /// <summary>
        /// The 'setNotificationPreferenceForAssetUpdatesError' Field.
        /// </summary>
        [JsonPropertyName("setNotificationPreferenceForAssetUpdatesError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? SetNotificationPreferenceForAssetUpdatesError { get; set; } = default;

    }
}
