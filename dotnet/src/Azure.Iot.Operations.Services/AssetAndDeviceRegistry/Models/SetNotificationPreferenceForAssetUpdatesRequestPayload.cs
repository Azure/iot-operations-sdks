namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class SetNotificationPreferenceForAssetUpdatesRequestPayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("notificationPreferenceRequest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public SetNotificationPreferenceForAssetUpdatesRequest NotificationPreferenceRequest { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (NotificationPreferenceRequest is null)
            {
                throw new ArgumentNullException("notificationPreferenceRequest field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (NotificationPreferenceRequest is null)
            {
                throw new ArgumentNullException("notificationPreferenceRequest field cannot be null");
            }
        }
    }
}
