namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class AssetManagementGroupActionSchemaElement : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// Configuration for the action.
        /// </summary>
        [JsonPropertyName("actionConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ActionConfiguration { get; set; } = default;

        /// <summary>
        /// Type of the action.
        /// </summary>
        [JsonPropertyName("actionType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public AssetManagementGroupActionType ActionType { get; set; } = default!;

        /// <summary>
        /// Name of the action.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Name { get; set; } = default!;

        /// <summary>
        /// TargetUri of action.
        /// </summary>
        [JsonPropertyName("targetUri")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string TargetUri { get; set; } = default!;

        /// <summary>
        /// Time out in seconds for the action
        /// </summary>
        [JsonPropertyName("timeOutInSeconds")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public uint? TimeOutInSeconds { get; set; } = default;

        /// <summary>
        /// The MQTT topic.
        /// </summary>
        [JsonPropertyName("topic")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Topic { get; set; } = default;

        /// <summary>
        /// URI or type definition id in companion spec.
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
