/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.Akri;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public partial class DataPointsSchemaElementSchema
    {
        /// <summary>
        /// The 'dataPointConfiguration' Field.
        /// </summary>
        [JsonPropertyName("dataPointConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DataPointConfiguration { get; set; } = default;

        /// <summary>
        /// The 'dataSource' Field.
        /// </summary>
        [JsonPropertyName("dataSource")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DataSource { get; set; } = default;

        /// <summary>
        /// The 'lastUpdatedOn' Field.
        /// </summary>
        [JsonPropertyName("lastUpdatedOn")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? LastUpdatedOn { get; set; } = default;

        /// <summary>
        /// The 'name' Field.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Name { get; set; } = default;

    }
}
