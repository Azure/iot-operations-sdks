namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class SetNotificationPreferenceForDeviceUpdatesRequestPayload
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("notificationPreferenceRequest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public NotificationPreference NotificationPreferenceRequest { get; set; } = default!;

    }
}
