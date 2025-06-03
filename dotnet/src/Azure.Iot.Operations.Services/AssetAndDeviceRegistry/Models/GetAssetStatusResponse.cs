namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class GetAssetStatusResponse
    {
        /// <summary>
        /// The 'assetStatus' Field.
        /// </summary>
        [JsonPropertyName("assetStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AssetStatus? AssetStatus { get; set; } = default;

        /// <summary>
        /// The 'getAssetStatusError' Field.
        /// </summary>
        [JsonPropertyName("getAssetStatusError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? GetAssetStatusError { get; set; } = default;

    }
}
