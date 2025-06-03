namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class AssetStatus
    {
        /// <summary>
        /// The 'config' Field.
        /// </summary>
        [JsonPropertyName("config")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AssetConfigStatus? Config { get; set; } = default;

        /// <summary>
        /// Array of dataset statuses that describe the status of each dataset.
        /// </summary>
        [JsonPropertyName("datasets")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<AssetDatasetEventStreamStatus>? Datasets { get; set; } = default;

        /// <summary>
        /// Array of event statuses that describe the status of each event.
        /// </summary>
        [JsonPropertyName("events")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<AssetDatasetEventStreamStatus>? Events { get; set; } = default;

        /// <summary>
        /// Array of management group statuses that describe the status of each management group.
        /// </summary>
        [JsonPropertyName("managementGroups")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<AssetManagementGroupStatusSchemaElement>? ManagementGroups { get; set; } = default;

        /// <summary>
        /// Array of stream statuses that describe the status of each stream.
        /// </summary>
        [JsonPropertyName("streams")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<AssetDatasetEventStreamStatus>? Streams { get; set; } = default;

    }
}
