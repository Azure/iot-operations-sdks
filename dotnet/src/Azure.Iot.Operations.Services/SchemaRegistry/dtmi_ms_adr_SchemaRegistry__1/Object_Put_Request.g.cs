/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class Object_Put_Request
    {
        /// <summary>
        /// The 'description' Field.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Description { get; set; } = default;

        /// <summary>
        /// The 'displayName' Field.
        /// </summary>
        [JsonPropertyName("displayName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DisplayName { get; set; } = default;

        /// <summary>
        /// The 'format' Field.
        /// </summary>
        [JsonPropertyName("format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Enum_Ms_Adr_SchemaRegistry_Format__1? Format { get; set; } = default;

        /// <summary>
        /// The 'schemaContent' Field.
        /// </summary>
        [JsonPropertyName("schemaContent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? SchemaContent { get; set; } = default;

        /// <summary>
        /// The 'schemaType' Field.
        /// </summary>
        [JsonPropertyName("schemaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Enum_Ms_Adr_SchemaRegistry_SchemaType__1? SchemaType { get; set; } = default;

        /// <summary>
        /// The 'tags' Field.
        /// </summary>
        [JsonPropertyName("tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, string>? Tags { get; set; } = default;

        /// <summary>
        /// The 'version' Field.
        /// </summary>
        [JsonPropertyName("version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Version { get; set; } = default;

    }
}
