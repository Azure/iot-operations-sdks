namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class AssetUpdateEvent : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'asset' Field.
        /// </summary>
        [JsonPropertyName("asset")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Asset Asset { get; set; } = default!;

        /// <summary>
        /// The 'assetName' Field.
        /// </summary>
        [JsonPropertyName("assetName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string AssetName { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Asset is null)
            {
                throw new ArgumentNullException("asset field cannot be null");
            }
            if (AssetName is null)
            {
                throw new ArgumentNullException("assetName field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Asset is null)
            {
                throw new ArgumentNullException("asset field cannot be null");
            }
            if (AssetName is null)
            {
                throw new ArgumentNullException("assetName field cannot be null");
            }
        }
    }
}
