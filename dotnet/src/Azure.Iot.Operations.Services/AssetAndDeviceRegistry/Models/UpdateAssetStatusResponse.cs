namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class UpdateAssetStatusResponse
    {
        /// <summary>
        /// The 'updateAssetStatusError' Field.
        /// </summary>
        [JsonPropertyName("updateAssetStatusError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? UpdateAssetStatusError { get; set; } = default;

        /// <summary>
        /// The 'updatedAssetStatus' Field.
        /// </summary>
        [JsonPropertyName("updatedAssetStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AssetStatus? UpdatedAssetStatus { get; set; } = default;

    }
}
