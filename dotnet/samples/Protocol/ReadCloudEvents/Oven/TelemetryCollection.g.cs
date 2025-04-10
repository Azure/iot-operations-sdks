/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace ReadCloudEvents.Oven
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using ReadCloudEvents;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class TelemetryCollection
    {
        /// <summary>
        /// The 'externalTemperature' Telemetry.
        /// </summary>
        [JsonPropertyName("externalTemperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public  double? ExternalTemperature { get; set; } = default

        /// <summary>
        /// The 'internalTemperature' Telemetry.
        /// </summary>
        [JsonPropertyName("internalTemperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public  double? InternalTemperature { get; set; } = default

        /// <summary>
        /// The 'operationSummary' Telemetry.
        /// </summary>
        [JsonPropertyName("operationSummary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public  OperationSummarySchema? OperationSummary { get; set; } = default

    }
}
