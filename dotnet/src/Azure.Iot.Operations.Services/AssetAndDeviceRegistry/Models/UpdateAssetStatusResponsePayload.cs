namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class UpdateAssetStatusResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("updatedAssetStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public AssetStatus UpdatedAssetStatus { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (UpdatedAssetStatus is null)
            {
                throw new ArgumentNullException("updatedAssetStatus field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (UpdatedAssetStatus is null)
            {
                throw new ArgumentNullException("updatedAssetStatus field cannot be null");
            }
        }
    }
}
