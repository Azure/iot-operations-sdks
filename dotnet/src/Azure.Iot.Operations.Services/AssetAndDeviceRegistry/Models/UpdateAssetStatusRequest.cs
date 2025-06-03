namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class UpdateAssetStatusRequest : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'assetName' Field.
        /// </summary>
        [JsonPropertyName("assetName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string AssetName { get; set; } = default!;

        /// <summary>
        /// The 'assetStatus' Field.
        /// </summary>
        [JsonPropertyName("assetStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public AssetStatus AssetStatus { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (AssetName is null)
            {
                throw new ArgumentNullException("assetName field cannot be null");
            }
            if (AssetStatus is null)
            {
                throw new ArgumentNullException("assetStatus field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (AssetName is null)
            {
                throw new ArgumentNullException("assetName field cannot be null");
            }
            if (AssetStatus is null)
            {
                throw new ArgumentNullException("assetStatus field cannot be null");
            }
        }
    }
}
