/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace SharedComplexSchemasSample.TelemetryComplexSchemas
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using SharedComplexSchemasSample;
    using SharedComplexSchemasSample.SharedSchemas;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public partial class TelemetryCollection
    {
        /// <summary>
        /// The 'coordinates' Telemetry.
        /// </summary>
        [JsonPropertyName("coordinates")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public CoordinatesSchema? Coordinates { get; set; } = default;

        /// <summary>
        /// The 'doubleArray2D' Telemetry.
        /// </summary>
        [JsonPropertyName("doubleArray2D")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<List<double>>? DoubleArray2d { get; set; } = default;

        /// <summary>
        /// The 'doubleMap' Telemetry.
        /// </summary>
        [JsonPropertyName("doubleMap")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, double>? DoubleMap { get; set; } = default;

        /// <summary>
        /// The 'doubleMapArray' Telemetry.
        /// </summary>
        [JsonPropertyName("doubleMapArray")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, List<double>>? DoubleMapArray { get; set; } = default;

        /// <summary>
        /// The 'proximity' Telemetry.
        /// </summary>
        [JsonPropertyName("proximity")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IntEnumProx? Proximity { get; set; } = default;

        /// <summary>
        /// The 'resultArray' Telemetry.
        /// </summary>
        [JsonPropertyName("resultArray")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<Results>? ResultArray { get; set; } = default;

        /// <summary>
        /// The 'speed' Telemetry.
        /// </summary>
        [JsonPropertyName("speed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SpeedSchema? Speed { get; set; } = default;

    }
}
