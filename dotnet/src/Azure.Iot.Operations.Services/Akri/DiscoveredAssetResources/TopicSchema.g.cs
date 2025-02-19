/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.8.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.8.0.0")]
    public partial class TopicSchema
    {
        /// <summary>
        /// The 'path' Field.
        /// </summary>
        [JsonPropertyName("path")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Path { get; set; } = default;

        /// <summary>
        /// The 'retain' Field.
        /// </summary>
        [JsonPropertyName("retain")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DiscoveredTopicRetain? Retain { get; set; } = default;

    }
}
