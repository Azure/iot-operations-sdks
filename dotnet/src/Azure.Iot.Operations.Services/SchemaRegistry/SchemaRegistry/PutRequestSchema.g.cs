/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.SchemaRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class PutRequestSchema
    {
        /// <summary>
        /// Human-readable description of the schema.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Description { get; set; } = default;

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        [JsonPropertyName("displayName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DisplayName { get; set; } = default;

        /// <summary>
        /// Format of the schema.
        /// </summary>
        [JsonPropertyName("format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Format? Format { get; set; } = default;

        /// <summary>
        /// Content stored in the schema.
        /// </summary>
        [JsonPropertyName("schemaContent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? SchemaContent { get; set; } = default;

        /// <summary>
        /// Type of the schema.
        /// </summary>
        [JsonPropertyName("schemaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SchemaType? SchemaType { get; set; } = default;

        /// <summary>
        /// Schema tags.
        /// </summary>
        [JsonPropertyName("tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, string>? Tags { get; set; } = default;

        /// <summary>
        /// Version of the schema. Allowed between 0-9.
        /// </summary>
        [JsonPropertyName("version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Version { get; set; } = default;

    }
}
