namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class CreateOrUpdateDiscoveredAssetRequest : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'discoveredAsset' Field.
        /// </summary>
        [JsonPropertyName("discoveredAsset")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public DiscoveredAsset DiscoveredAsset { get; set; } = default!;

        /// <summary>
        /// The 'discoveredAssetName' Field.
        /// </summary>
        [JsonPropertyName("discoveredAssetName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string DiscoveredAssetName { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (DiscoveredAsset is null)
            {
                throw new ArgumentNullException("discoveredAsset field cannot be null");
            }
            if (DiscoveredAssetName is null)
            {
                throw new ArgumentNullException("discoveredAssetName field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (DiscoveredAsset is null)
            {
                throw new ArgumentNullException("discoveredAsset field cannot be null");
            }
            if (DiscoveredAssetName is null)
            {
                throw new ArgumentNullException("discoveredAssetName field cannot be null");
            }
        }
    }
}
