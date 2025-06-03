namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class GetAssetResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("asset")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Asset Asset { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Asset is null)
            {
                throw new ArgumentNullException("asset field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Asset is null)
            {
                throw new ArgumentNullException("asset field cannot be null");
            }
        }
    }
}
