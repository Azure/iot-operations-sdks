/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class EventsSchemaElementSchema
    {
        /// <summary>
        /// The 'eventConfiguration' Field.
        /// </summary>
        [JsonPropertyName("eventConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public  string? EventConfiguration { get; set; } = default

        /// <summary>
        /// The 'eventNotifier' Field.
        /// </summary>
        [JsonPropertyName("eventNotifier")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public  string? EventNotifier { get; set; } = default

        /// <summary>
        /// The 'lastUpdatedOn' Field.
        /// </summary>
        [JsonPropertyName("lastUpdatedOn")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public  string? LastUpdatedOn { get; set; } = default

        /// <summary>
        /// The 'name' Field.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public  string? Name { get; set; } = default

        /// <summary>
        /// The 'topic' Field.
        /// </summary>
        [JsonPropertyName("topic")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public  TopicSchema? Topic { get; set; } = default

    }
}
