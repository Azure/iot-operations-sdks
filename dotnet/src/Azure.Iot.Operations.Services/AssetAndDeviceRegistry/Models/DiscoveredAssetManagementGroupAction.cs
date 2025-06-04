namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class DiscoveredAssetManagementGroupAction : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'actionConfiguration' Field.
        /// </summary>
        [JsonPropertyName("actionConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonDocument? ActionConfiguration { get; set; } = default;

        /// <summary>
        /// The 'actionType' Field.
        /// </summary>
        [JsonPropertyName("actionType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public AssetManagementGroupActionType ActionType { get; set; } = default!;

        /// <summary>
        /// The 'lastUpdatedOn' Field.
        /// </summary>
        [JsonPropertyName("lastUpdatedOn")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime? LastUpdatedOn { get; set; } = default;

        /// <summary>
        /// The 'name' Field.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Name { get; set; } = default!;

        /// <summary>
        /// The 'targetUri' Field.
        /// </summary>
        [JsonPropertyName("targetUri")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string TargetUri { get; set; } = default!;

        /// <summary>
        /// The 'timeOutInSeconds' Field.
        /// </summary>
        [JsonPropertyName("timeOutInSeconds")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public uint? TimeOutInSeconds { get; set; } = default;

        /// <summary>
        /// The 'topic' Field.
        /// </summary>
        [JsonPropertyName("topic")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Topic { get; set; } = default;

        /// <summary>
        /// The 'typeRef' Field.
        /// </summary>
        [JsonPropertyName("typeRef")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? TypeRef { get; set; } = default;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
            if (TargetUri is null)
            {
                throw new ArgumentNullException("targetUri field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
            if (TargetUri is null)
            {
                throw new ArgumentNullException("targetUri field cannot be null");
            }
        }
    }
}
