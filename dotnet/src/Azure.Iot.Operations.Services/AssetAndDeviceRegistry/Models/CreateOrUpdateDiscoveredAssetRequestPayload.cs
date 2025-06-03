namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class CreateOrUpdateDiscoveredAssetRequestPayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("discoveredAssetRequest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public CreateOrUpdateDiscoveredAssetRequest DiscoveredAssetRequest { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DiscoveredAssetRequest is null)
            {
                throw new ArgumentNullException("discoveredAssetRequest field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DiscoveredAssetRequest is null)
            {
                throw new ArgumentNullException("discoveredAssetRequest field cannot be null");
            }
        }
    }
}
