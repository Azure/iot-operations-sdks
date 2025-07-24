namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class SetNotificationPreferenceForDeviceUpdatesResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("responsePayload")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string ResponsePayload { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (ResponsePayload is null)
            {
                throw new ArgumentNullException("responsePayload field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (ResponsePayload is null)
            {
                throw new ArgumentNullException("responsePayload field cannot be null");
            }
        }
    }
}
