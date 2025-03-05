/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace SharedComplexSchemasSample.TelemetryComplexSchemas
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using SharedComplexSchemasSample;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public partial class CoordinatesSchema
    {
        /// <summary>
        /// The 'latitude' Field.
        /// </summary>
        [JsonPropertyName("latitude")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double? Latitude { get; set; } = default;

        /// <summary>
        /// The 'longitude' Field.
        /// </summary>
        [JsonPropertyName("longitude")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double? Longitude { get; set; } = default;

    }
}
