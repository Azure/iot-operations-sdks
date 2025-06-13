namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class CreateOrUpdateDiscoveredAssetResponseSchema
    {
        /// <summary>
        /// The 'createOrUpdateDiscoveredAssetError' Field.
        /// </summary>
        [JsonPropertyName("createOrUpdateDiscoveredAssetError")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AkriServiceError? CreateOrUpdateDiscoveredAssetError { get; set; } = default;

        /// <summary>
        /// The 'discoveredAssetResponse' Field.
        /// </summary>
        [JsonPropertyName("discoveredAssetResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DiscoveredAssetResponseSchema? DiscoveredAssetResponse { get; set; } = default;

    }
}
