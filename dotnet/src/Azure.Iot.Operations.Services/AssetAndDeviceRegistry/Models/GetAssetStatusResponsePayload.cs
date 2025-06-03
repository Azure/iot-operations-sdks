namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class GetAssetStatusResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("assetStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public AssetStatus AssetStatus { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (AssetStatus is null)
            {
                throw new ArgumentNullException("assetStatus field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (AssetStatus is null)
            {
                throw new ArgumentNullException("assetStatus field cannot be null");
            }
        }
    }
}
