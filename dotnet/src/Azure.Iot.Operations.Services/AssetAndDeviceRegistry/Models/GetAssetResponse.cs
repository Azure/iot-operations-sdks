namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class GetAssetResponse
    {
        /// <summary>
        /// The 'asset' Field.
        /// </summary>
        [JsonPropertyName("asset")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Asset? Asset { get; set; } = default;

        /// <summary>
        /// The 'getAssetError' Field.
        /// </summary>
        [JsonPropertyName("getAssetError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? GetAssetError { get; set; } = default;

    }
}
