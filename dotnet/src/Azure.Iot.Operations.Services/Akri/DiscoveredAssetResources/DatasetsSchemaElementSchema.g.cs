/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public partial class DatasetsSchemaElementSchema
    {
        /// <summary>
        /// The 'dataPoints' Field.
        /// </summary>
        [JsonPropertyName("dataPoints")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<DataPointsSchemaElementSchema>? DataPoints { get; set; } = default;

        /// <summary>
        /// The 'dataSetConfiguration' Field.
        /// </summary>
        [JsonPropertyName("dataSetConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DataSetConfiguration { get; set; } = default;

        /// <summary>
        /// The 'name' Field.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Name { get; set; } = default;

        /// <summary>
        /// The 'topic' Field.
        /// </summary>
        [JsonPropertyName("topic")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TopicSchema? Topic { get; set; } = default;

    }
}
