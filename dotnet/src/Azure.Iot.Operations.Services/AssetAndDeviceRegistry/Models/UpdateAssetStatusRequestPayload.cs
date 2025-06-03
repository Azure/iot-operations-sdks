namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class UpdateAssetStatusRequestPayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("assetStatusUpdate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public UpdateAssetStatusRequest AssetStatusUpdate { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (AssetStatusUpdate is null)
            {
                throw new ArgumentNullException("assetStatusUpdate field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (AssetStatusUpdate is null)
            {
                throw new ArgumentNullException("assetStatusUpdate field cannot be null");
            }
        }
    }
}
