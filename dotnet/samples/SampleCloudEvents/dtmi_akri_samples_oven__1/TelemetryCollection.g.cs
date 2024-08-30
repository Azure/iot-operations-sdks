/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace SampleCloudEvents.dtmi_akri_samples_oven__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class TelemetryCollection
    {
        /// <summary>
        /// The 'externalTemperature' Telemetry.
        /// </summary>
        [JsonPropertyName("externalTemperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double? ExternalTemperature { get; set; } = default;

        /// <summary>
        /// The 'internalTemperature' Telemetry.
        /// </summary>
        [JsonPropertyName("internalTemperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double? InternalTemperature { get; set; } = default;

        /// <summary>
        /// The 'operationSummary' Telemetry.
        /// </summary>
        [JsonPropertyName("operationSummary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Object_OperationSummary? OperationSummary { get; set; } = default;

    }
}
