namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class AssetManagementGroupSchemaElement : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// Actions for this management group.
        /// </summary>
        [JsonPropertyName("actions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<AssetManagementGroupActionSchemaElement>? Actions { get; set; } = default;

        /// <summary>
        /// Default time out in seconds for the management group.
        /// </summary>
        [JsonPropertyName("defaultTimeOutInSeconds")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public uint? DefaultTimeOutInSeconds { get; set; } = default;

        /// <summary>
        /// The default MQTT topic for the management group.
        /// </summary>
        [JsonPropertyName("defaultTopic")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DefaultTopic { get; set; } = default;

        /// <summary>
        /// Configuration for the mangement group.
        /// </summary>
        [JsonPropertyName("managementGroupConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ManagementGroupConfiguration { get; set; } = default;

        /// <summary>
        /// Name of the management group.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Name { get; set; } = default!;

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
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
        }
    }
}
