namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class CreateOrUpdateDiscoveredAssetResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("discoveredAssetResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DiscoveredAssetResponseSchema DiscoveredAssetResponse { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DiscoveredAssetResponse is null)
            {
                throw new ArgumentNullException("discoveredAssetResponse field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DiscoveredAssetResponse is null)
            {
                throw new ArgumentNullException("discoveredAssetResponse field cannot be null");
            }
        }
    }
}
