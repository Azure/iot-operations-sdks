namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class AssetUpdateEventTelemetry : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'assetUpdateEvent' Telemetry.
        /// </summary>
        [JsonPropertyName("assetUpdateEvent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public AssetUpdateEvent AssetUpdateEvent { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (AssetUpdateEvent is null)
            {
                throw new ArgumentNullException("assetUpdateEvent field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (AssetUpdateEvent is null)
            {
                throw new ArgumentNullException("assetUpdateEvent field cannot be null");
            }
        }
    }
}
