namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DiscoveredAssetResponse : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'discoveryId' Field.
        /// </summary>
        [JsonPropertyName("discoveryId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string DiscoveryId { get; set; } = default!;

        /// <summary>
        /// The 'version' Field.
        /// </summary>
        [JsonPropertyName("version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public ulong Version { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DiscoveryId is null)
            {
                throw new ArgumentNullException("discoveryId field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DiscoveryId is null)
            {
                throw new ArgumentNullException("discoveryId field cannot be null");
            }
        }
    }
}
